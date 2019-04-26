namespace Server
open System.Net.Http
open Auth0.ManagementApi

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
            @"C:\Users\1\RiderProjects\famil.io\src\Server\appsettings.json").Build()


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

    }

    let configureSerialization (services:IServiceCollection) =
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
