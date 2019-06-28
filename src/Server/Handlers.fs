namespace Server
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks.V2
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open Giraffe
open System
open System.Net.Http
open Thoth.Json.Net
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Logging
open Auth0.ManagementApi
open Shared
open Db
open System.Linq
open Auth0.ManagementApi.Models
open Shared.Dto
open LinqToDB
open Tables
open Saturn


type TokenResponse =
    { access_token: string
      expires_in: int64
      scope: string
      token_type: string }

module Option =    
    let insert (db: DbFamilio) arg =
        task {
            let! inserted = db.InsertAsync(arg)
            return Some inserted
        }
    let taskNone<'a> : Threading.Tasks.Task<'a option> =
        task {
            return None
        }


module Account =
    
    let private getProps uri =
        AuthenticationProperties(
            RedirectUri=(if String.IsNullOrEmpty uri then
                            "http://localhost:8085/api/callback"
                        else uri))    

    let getApiToken (configuration: IConfigurationRoot) =
        task {
            use client = new HttpClient()
            let s =
                sprintf
                    "grant_type=client_credentials&client_id=%s&client_secret=%s&audience=%s"
                    configuration.["Auth0:ClientId"]
                    configuration.["Auth0:ClientSecret"]
                    configuration.["Auth0:Identifier"]
            use content = new StringContent(s)
            content.Headers.Remove("content-type") |> ignore
            content.Headers.Add("content-type", "application/x-www-form-urlencoded")
            let! resp = client.PostAsync(Uri(configuration.["Auth0:TokenUri"]), content)
            let! content = resp.Content.ReadAsStringAsync()
            return content |> Decode.Auto.fromString<TokenResponse>
        }


    let getAuth0Client (configuration: IConfigurationRoot) =
        task {
            let! token =
                task {
                    match! getApiToken configuration with
                    | Error e -> raise <| InvalidOperationException(e); return ""
                    | Ok r -> return r.access_token
                }
            return new ManagementApiClient(token,
                                 Uri(configuration.["Auth0:Identifier"]))
        }

    let login uri (next: HttpFunc) (ctx: HttpContext) =
        task{
                do! ctx.ChallengeAsync("Auth0", getProps uri)
                return! json null next ctx
            }

    let logout uri (next: HttpFunc) (ctx: HttpContext) =
        task{            
            do! ctx.SignOutAsync("Auth0", getProps uri)
            do! ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            return! json null next ctx
        }
    let currentUser (configuration: IConfigurationRoot) (next: HttpFunc) (ctx: HttpContext) =
        task {
            use! auth0Client = getAuth0Client configuration
            let logger = ctx.GetLogger<string>()
            ctx.SetHttpHeader "Access-Control-Allow-Origin" "*"
            let user = ctx.User.Claims |> Seq.tryHead
            let id = user |> function | Some x -> x.Value | None -> ""
            let! resp =
                auth0Client.Users.GetAsync(id, "user_id,nickname,email", true)
            let user = { Id = resp.UserId |> Some; Nickname = resp.NickName |> Some ; Email = resp.Email |> Some }
            logger.Log(LogLevel.Trace, EventId(0, "user"), sprintf "Got User: %A" user)
            return! json user next ctx
        }
    let callback next ctx =
            task {
                return! json null next ctx
                }

module Groups =

    let groupsByUserId userId next ctx =
        task {
            use db = new DbFamilio()
            let groups =
                query {
                    for urg in db.UsersRolesGroups do
                        join g in db.Groups on (urg.GroupId = g.Id)
                        where (urg.UserId = userId)
                        groupBy g
                }
            let res =
                groups.ToArray()
                |> Array.filter (fun g -> Option.isNone g.Key.DeletedAt && Option.isNone g.Key.DeletedBy)
                |> Array.map (fun g -> g.Key)
            return! json res next ctx
        }

    let groupById configuration id next ctx =
        task{
            use db = new DbFamilio()
            let group =
                query{
                    for g in db.Groups do
                    where (g.Id = id)
                    select g
                } |> Seq.tryExactlyOne
            let tasks =
                query{
                    for t in db.Tasks do
                    where (t.GroupId = id &&
                           (not t.ExpiresBy.HasValue || t.ExpiresBy.Value > DateTime.Now.ToUniversalTime()) &&
                           (t.Status <> Status.Closed))
                    select t
                } |> Seq.toArray
            let users =
                query{
                    for ugr in db.UsersRolesGroups do
                    where (ugr.GroupId = id)
                    groupBy ugr.UserId
                } |> Seq.toArray
            use! auth0Mgr = Account.getAuth0Client configuration
            let users =
                users
                |> Array.map ((fun u ->
                    auth0Mgr.Users.GetAsync(u.Key, "user_id,nickname,email", true)
                    |> Async.AwaitTask
                    |> Async.RunSynchronously)
                  >> (fun resp ->
                    { Id = resp.UserId |> Option.fromString
                      Nickname = resp.NickName |> Option.fromString
                      Email = resp.Email |> Option.fromString }) )
            let tasksAndUsers =
                tasks
                |> Array.map (fun t ->
                    t, auth0Mgr.Users.GetAsync(t.Executor, "nickname", true)
                    |> Async.AwaitTask
                    |> Async.RunSynchronously
                    |> (fun u -> u.NickName) )
            return! json (group, tasksAndUsers, users) next ctx
        }
    let searchUserForGroup configuration ((groupId: int64), nick) next ctx =
        task{
            use! auth0Client = Account.getAuth0Client configuration
            use db = new DbFamilio()
            let req =
                GetUsersRequest(
                        SearchEngine="v3",
                        IncludeFields=Nullable(true),
                        Fields="user_id,nickname,email",
                        Query=(sprintf "nickname:%s" (Shared.Functions.prepare nick))
                    )
            let! us = auth0Client.Users.GetAllAsync(req, PaginationInfo())
            let existingUsers =
                query {
                    for urg in db.UsersRolesGroups do
                        where (urg.GroupId = groupId)
                } |> Seq.toArray
            let users =
                us.AsEnumerable()
                |> Seq.map ((fun u ->
                      { Id = u.UserId |> Option.ofObj
                        Nickname = u.NickName |> Option.ofObj
                        Email = u.Email |> Option.ofObj })
                      >> (fun u -> (u, false)))
                |> Seq.filter ( fun (u,_) -> existingUsers.Any (fun urg -> urg.UserId = (u.Id |> Option.defaultValue "")) |> not )
            return! json (nick, users) next ctx
        }

    let createGroup next (ctx: HttpContext) =
        task {
            let! body = ctx.ReadBodyFromRequestAsync()
            let res = Decode.Auto.fromString<GroupDto> body
            let group =
                match res with
                | Error _ -> raise <| InvalidOperationException("")
                | Ok g -> g
            use db = new DbFamilio()
            let newGroup =
                { Id = 0L
                  Name = group.Name
                  CreatedAt = group.CreatedAt
                  CreatedBy = group.CreatedBy
                  DeletedAt = group.DeletedAt
                  DeletedBy = group.DeletedBy }
            use! tran =  db.BeginTransactionAsync()
            let! insertedGroupId = db.InsertWithInt64IdentityAsync(newGroup)
            let urg =
                { UserId = newGroup.CreatedBy
                  GroupId = insertedGroupId
                  RoleId = (Roles.Admin |> int) }
            let! _ = db.InsertAsync(urg)
            do! tran.CommitAsync()
            return! json insertedGroupId next ctx
        }

    let addUserToGroup nick next (ctx: HttpContext) =
        task {
            let! body = ctx.ReadBodyFromRequestAsync()
            let res = Decode.Auto.fromString<UsersRolesGroupsDto> body
            let urg = 
                match res with 
                | Ok urg  -> urg
                | Error msg -> raise <| InvalidOperationException msg                    
            use db = new DbFamilio()
            let groupExists =
                query {
                    for g in db.Groups do
                        where (g.Id = urg.GroupId)
                        select g
                } 
                |> Seq.tryHead
                |> Option.isSome
            let urg = 
                { UserId = urg.UserId
                  RoleId = urg.RoleId
                  GroupId = urg.GroupId }
            let insert = Option.insert db
            let! inserted = if groupExists then insert urg else Option.taskNone
            return! json (nick, inserted) next ctx
        }

module Tasks =

    let createTask next (ctx: HttpContext) =
        task{
            use db = new DbFamilio()
            let! body = ctx.ReadBodyFromRequestAsync()
            let res = Decode.Auto.fromString<TaskDto> body
            let task =
                match res with
                | Ok t -> t
                | Error m -> raise <| InvalidOperationException m
            let newTask =
                { Id = System.Guid.NewGuid()
                  GroupId = task.GroupId
                  Name = task.Name
                  Description = task.Description |> Option.toObj
                  CreatedBy = task.CreatedBy
                  CreatedAt = task.CreatedAt
                  Executor = task.Executor
                  ExpiresBy = task.ExpiresBy |> Option.toNullable
                  Status = enum task.Status
                  Priority = enum task.Priority }
            let! inserted = db.InsertAsync(newTask)
            return! json inserted next ctx
        }
        
    let editTask next (ctx: HttpContext) =
        task {
            let! body = ctx.ReadBodyFromRequestAsync()
            let res = Decode.Auto.fromString<TaskDto> body
            let taskDto = 
                match res with 
                | Ok t -> t
                | Error m -> raise <| InvalidOperationException m
            let task = 
                { Id = taskDto.Id
                  GroupId = taskDto.GroupId
                  Name = taskDto.Name
                  Description = taskDto.Description |> Option.toObj
                  CreatedBy = taskDto.CreatedBy
                  CreatedAt = taskDto.CreatedAt
                  Executor = taskDto.Executor
                  ExpiresBy = taskDto.ExpiresBy |> Option.toNullable
                  Status = enum taskDto.Status
                  Priority = enum taskDto.Priority }
            use db = new DbFamilio()
            let! _ = db.UpdateAsync(task)
            return! json null next ctx
        }

    let closeTask (id: string) next ctx =
        task {
            let id = Guid.Parse id
            use db = new DbFamilio()
            let update arg =
                task {
                    let! updated = db.UpdateAsync(arg)
                    return Some updated
                }
            let none = task {return None}
            let task = 
                query {
                    for t in db.Tasks do
                        where (t.Id = id)
                        select t
                } 
                |> Seq.tryHead
                |> Option.map (fun t -> {t with Status = Status.Closed})
            let! _ = match task with | Some t -> update t | None -> none
            let resp = match task with | Some t -> json t next | None -> ((fun t ctx -> Response.badRequest ctx t) null)
            return! resp ctx
        }