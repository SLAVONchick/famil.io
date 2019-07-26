namespace Server
open LinqToDB.Configuration
open LinqToDB.Data
open Server.Db
open System.Net
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.HttpOverrides
open Microsoft.AspNetCore.Authentication.OpenIdConnect
open System.IO
open System.Threading.Tasks
open Microsoft.Extensions.Configuration;
open Microsoft.Extensions.DependencyInjection;
open Microsoft.Extensions.Logging;
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Authentication.Cookies
open System
open Saturn
open Shared



module Startup =

    let tryGetEnv = System.Environment.GetEnvironmentVariable >> Option.fromString

    let publicPath = match tryGetEnv "STATIC_FILES" with | None -> Path.GetFullPath "../Client/" | Some v -> v

    let proxyIpAddress = (match tryGetEnv "PROXY_IP" with | None -> "0.0.0.0" | Some ip -> ip) |> IPAddress.Parse

    let configuration =
        ConfigurationBuilder()
            .AddJsonFile(@"./appsettings.json")
            .Build()

    let port = "SERVER_PORT" |> tryGetEnv |> Option.map uint16 |> Option.defaultValue 8085us

    let webApp =
        router {
            getf "/api/login/%s" Account.login
            getf "/api/logout/%s" Account.logout
            get "/api/users/current" (Account.currentUser configuration)
            post "/api/callback" Account.callback
            getf "/api/groups/%s" Groups.groupsByUserId
            getf "/api/group/%d" (Groups.groupById configuration)
            getf "/api/group/%d/user/%s" (Groups.searchUserForGroup configuration)
            post "/api/group" Groups.createGroup
            postf "/api/group/user/%s" Groups.addUserToGroup
            post "/api/task" Tasks.createTask             
            put "/api/task" Tasks.editTask            
            deletef "/api/task/%s" Tasks.closeTask
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
        log.AddConsole()
            .AddDebug()
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