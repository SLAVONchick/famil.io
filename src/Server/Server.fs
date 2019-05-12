namespace Server
open System.Collections.Generic
open System.Net.Http
open Auth0.ManagementApi
open LinqToDB.Configuration
open LinqToDB.Data
open LinqToDB.Linq
open System.Collections.Generic
open System.Linq
open Server.Db
open Shared.Dto
open LinqToDB
open Thoth.Json.Net

module Seq =
  let tryHead s =
    try
      s |> Seq.head |> Some
    with _ ->
      None

module Startup =
    open System.IO
    open System.Threading.Tasks
    open Microsoft.AspNetCore.Builder;
    open Microsoft.AspNetCore.Hosting;
    open Microsoft.Extensions.Configuration;
    open Microsoft.Extensions.DependencyInjection;
    open Microsoft.Extensions.Logging;
    open Microsoft.AspNetCore.Http
    open Microsoft.AspNetCore.Authentication.OpenIdConnect
    open Microsoft.AspNetCore.Authentication.Cookies
    open System
    open Microsoft.AspNetCore.Authentication
    open FSharp.Control.Tasks.V2
    open Giraffe
    open Saturn
    open Shared

    let tryGetEnv = System.Environment.GetEnvironmentVariable >> function null | "" -> None | x -> Some x

    let publicPath = Path.GetFullPath "../Client/public"

    let configuration =
        ConfigurationBuilder().AddJsonFile(
            //"/home/viacheslav/repositories/famil.io/src/Server/appsettings.json"
            @"./appsettings.json").Build()


    let auth0Client =
        new ManagementApiClient(configuration.["Auth0:Token"],
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
    let webApp = router {
        get "/api/init" (fun next ctx ->
            task {
                let! counter = getInitCounter()
                return! json counter next ctx
            })
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
        post "/api/callback" (fun next ctx ->
            task {
                return! json null next ctx
            })

        get "/api/currentuser" (fun next ctx ->
            task {
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
                    |> Array.map (fun g ->
                        { Id = g.Key.Id
                          Name = g.Key.Name
                          CreatedAt = g.Key.CreatedAt
                          CreatedBy = g.Key.CreatedBy
                          DeletedBy = g.Key.DeletedBy
                          DeletedAt = g.Key.DeletedAt })
                return! json res next ctx
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
                let inserted = db.Insert(urg)
                do! tran.CommitAsync()
                return! json insertedGroupId next ctx
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
        services.AddAuthentication(fun o ->
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
        app.UseDeveloperExceptionPage()
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
