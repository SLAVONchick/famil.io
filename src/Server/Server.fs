namespace Server
open System.Net.Http
open Auth0.ManagementApi
open LinqToDB.Configuration
open LinqToDB.Data
open System.Linq
open Server.Db
open Shared.Dto
open LinqToDB
open Thoth.Json.Net
open Auth0.ManagementApi.Models
open System.Net
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.HttpOverrides

module Seq =
  let tryHead s =
    try
      s |> Seq.head |> Some
    with _ ->
      None

module Startup =
    open System.IO
    open System.Threading.Tasks
    open Microsoft.AspNetCore.Hosting;
    open Microsoft.Extensions.Configuration;
    open Microsoft.Extensions.DependencyInjection;
    open Microsoft.Extensions.Logging;
    open Microsoft.AspNetCore.Http
    open Microsoft.AspNetCore.Authentication.Cookies
    open System
    open Microsoft.AspNetCore.Authentication
    open FSharp.Control.Tasks.V2
    open Giraffe
    open Saturn
    open Shared

    let inline (|IsBigEnough|_|) len (input:^a) =
        let inputLen = (^a: (member Length : int) input)
        if inputLen >= len then Some () else None

    let prepare (nick: string) =
        match nick with
        | IsBigEnough 3 -> "*" + nick + "*"
        | _ -> nick + "*"

    type TokenResponse =
        { access_token: string
          expires_in: int64
          scope: string
          token_type: string }

    let tryGetEnv = System.Environment.GetEnvironmentVariable >> function null | "" -> None | x -> Some x

    let publicPath = match tryGetEnv "STATIC_FILES" with | None -> Path.GetFullPath "../Client/" | Some v -> v

    let proxyIpAddress = (match tryGetEnv "PROXY_IP" with | None -> "0.0.0.0" | Some ip -> ip) |> IPAddress.Parse

    let configuration =
        ConfigurationBuilder().AddJsonFile(
            //"/home/viacheslav/repositories/famil.io/src/Server/appsettings.json"
            @"./appsettings.json").Build()

    let getApiToken () =
        use client = new HttpClient()
        let s =
            sprintf
                "grant_type=client_credentials&client_id=%s&client_secret=%s&audience=%s"
                configuration.["Auth0:ClientId"]
                configuration.["Auth0:ClientSecret"]
                configuration.["Auth0:Identifier"]
        printfn "%s" s
        use content = new StringContent(s)
        content.Headers.Remove("content-type") |> ignore
        content.Headers.Add("content-type", "application/x-www-form-urlencoded")
        let resp =
            client.PostAsync(Uri(configuration.["Auth0:TokenUri"]), content)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        resp.Content.ReadAsStringAsync()
        |> Async.AwaitTask
        |> Async.RunSynchronously
        |> Decode.Auto.fromString<TokenResponse>


    let getAuth0Client () =
        let token =
            match getApiToken() with
            | Error e -> raise <| InvalidOperationException(e)
            | Ok r -> r.access_token
        new ManagementApiClient(token,
                                 Uri(configuration.["Auth0:Identifier"]))

    let port = "SERVER_PORT" |> tryGetEnv |> Option.map uint16 |> Option.defaultValue 8085us

    let getInitCounter() : Task<Counter> = task { return { Value = 42 } }

    let logout (ctx: HttpContext) props =
        task{
                do! ctx.SignOutAsync("Auth0", props)
                do! ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)
                return ()
            }

    let login (ctx: HttpContext) props =
        task{
                do! ctx.ChallengeAsync("Auth0", props)
                return ()
            }

    open Server.Tables
    let webApp =
        router {
            getf "/api/login/%s" (fun uri next ctx ->
                task{
                    let props =
                        AuthenticationProperties(
                            RedirectUri=(if String.IsNullOrEmpty uri then
                                            "http://localhost:8085/api/callback"
                                         else uri))
                    do! login ctx props
                    return! json null next ctx
                })

            getf "/api/logout/%s" (fun uri next ctx ->
                task{
                    let props =
                        AuthenticationProperties(
                            RedirectUri=(if String.IsNullOrEmpty uri then
                                            "http://localhost:8085/api/callback"
                                         else uri))
                    do! logout ctx props
                    return! json null next ctx
                })

            get "/api/users/current" (fun next ctx ->
                task {
                    use auth0Client = getAuth0Client ()
                    ctx.SetHttpHeader "Access-Control-Allow-Origin" "*"
                    let user = ctx.User.Claims |> Seq.tryHead
                    let id = user |> function | Some x -> x.Value | None -> ""
                    let! resp =
                        auth0Client.Users.GetAsync(id, "user_id,nickname,email", true)
                    let user = { Id = resp.UserId |> Some; Nickname = resp.NickName |> Some ; Email = resp.Email |> Some }
                    return! json user next ctx
                })

            getf "/api/groups/%s" (fun userId next ctx ->
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
                })

            getf "/api/group/%d" (fun id next ctx ->
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
                            where (t.GroupId = id)
                            select t
                        } |> Seq.toArray
                    let users =
                        query{
                            for ugr in db.UsersRolesGroups do
                            where (ugr.GroupId = id)
                            groupBy ugr.UserId
                        } |> Seq.toArray
                    use auth0Mgr = getAuth0Client()
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
                })

            getf "/api/group/%d/user/%s" (fun ((groupId: int64), nick) next ctx ->
                task{
                    use auth0Client = getAuth0Client ()
                    use db = new DbFamilio()
                    let req =
                        GetUsersRequest(
                                SearchEngine="v3",
                                IncludeFields=Nullable(true),
                                Fields="user_id,nickname,email",
                                Query=(sprintf "nickname:%s" (prepare nick))
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
                })
                
            post "/api/callback" (fun next ctx ->
                task {
                    return! json null next ctx
                })

            post "/api/group" (fun next ctx ->
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
                })

            post "/api/task" (fun next ctx ->
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
                })

            postf "/api/group/user/%s" (fun nick next ctx -> 
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
                    let insert arg =
                        task {
                            let! inserted = db.InsertAsync(arg)
                            return Some inserted
                        }
                    let! inserted = if groupExists then insert urg else task { return None }
                    return! json (nick, inserted) next ctx
                })
    }

    let configureSerialization (services:IServiceCollection) =
        let settings = seq {
            yield ConnectionStringSettings(configuration.["ConnectionStrings:familio"], Db.dbName)
            :> IConnectionStringSettings
        }
        let providers = System.Linq.Enumerable.Empty<IDataProviderSettings>()
        let postgre = LinqToDB.ProviderName.PostgreSQL
        DataConnection.DefaultSettings <- DbSettings(providers, postgre, postgre, settings)
        services.AddSingleton<Giraffe.Serialization.Json.IJsonSerializer>(Thoth.Json.Giraffe.ThothSerializer())
        |> ignore
        services
         .AddAuthentication(fun o ->
            o.DefaultAuthenticateScheme <- CookieAuthenticationDefaults.AuthenticationScheme;
            o.DefaultSignInScheme <- CookieAuthenticationDefaults.AuthenticationScheme;
            o.DefaultChallengeScheme <- CookieAuthenticationDefaults.AuthenticationScheme;
        )
         .AddCookie(fun o ->
            o.Cookie.Name <- "Interop")
         .AddOpenIdConnect(
         "Auth0", fun o ->
              let domain = configuration.["Auth0:Domain"]
              let clientId = configuration.["Auth0:ClientId"]
              let clientSecret = configuration.["Auth0:ClientSecret"]
              let apiId = configuration.["Auth0:Identifier"]
              o.Authority <- sprintf "https://%s" domain
              o.ClientId <- clientId
              o.ClientSecret <- clientSecret
              o.ResponseType <- "code"
              o.Scope.Clear()
              o.Scope.Add("openid")
              o.CallbackPath <- PathString("/api/callback")
              o.ClaimsIssuer <- "Auth0"
              o.SaveTokens <- true
              let events = OpenIdConnectEvents()
              events.OnRedirectToIdentityProviderForSignOut <- (fun (ctx:RedirectContext) ->
                    let logoutUri =
                        sprintf "https://%s/v2/logout?client_id=%s" domain clientId
                    let postLogoutUri = ctx.Properties.RedirectUri |> Option.ofObj
                    match postLogoutUri with
                    | Some uri ->
                        match uri.StartsWith("/") with
                        | true ->
                            let req = ctx.Request
                            req.Scheme + "://" + req.Host.Host + req.PathBase.Value + uri
                        | _ -> uri
                        |> Uri.EscapeDataString
                        |> sprintf "%s&returnTo=%s" logoutUri
                    | None -> ""
                    |> ctx.Response.Redirect
                    ctx.HandleResponse()
                    ctx.ProtocolMessage.SetParameter("audience", apiId) |> ignore
                    Task.CompletedTask)
              o.Events <- events)
         |> ignore
        services

    let configureApp(app: IApplicationBuilder) =
        app.UseForwardedHeaders(ForwardedHeadersOptions(
                                    ForwardedHeaders=(ForwardedHeaders.XForwardedFor ||| ForwardedHeaders.XForwardedProto)
                                    )
                               )
           .UseDeveloperExceptionPage()
           .UseAuthentication()

    let configureLogging(log: ILoggingBuilder) =
        log.AddConsole().AddDebug()
        |> ignore

    let app = application {
        url ("http://0.0.0.0:" + port.ToString() + "/")
        use_router webApp
        memory_cache
        use_static publicPath
        app_config configureApp
        logging configureLogging
        service_config configureSerialization
        use_gzip
    }

    run app