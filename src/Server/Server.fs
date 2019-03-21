namespace Server
open LinqToDB.Identity
open LinqToDB.Identity
open Microsoft.AspNetCore.Identity
open Microsoft.AspNetCore.DataProtection
open Microsoft.AspNetCore.Identity
open LinqToDB.Identity
open LinqToDB.Identity
open LinqToDB.Identity
open LinqToDB.Identity
open LinqToDB.Common
open LinqToDB.DataProvider.PostgreSQL

module Startup =
    open System.IO
    open System.Threading.Tasks
    open System.Data.SqlClient;
    open System.IO;
    //open IdentitySample.Models;
    //open IdentitySample.Services;
    open LinqToDB;
    open LinqToDB.Data;
    open LinqToDB.Identity;
    open Microsoft.AspNetCore.Builder;
    open Microsoft.AspNetCore.DataProtection;
    open Microsoft.AspNetCore.Hosting;
    open Microsoft.AspNetCore.Identity;
    open Microsoft.Extensions.Configuration;
    open Microsoft.Extensions.DependencyInjection;
    open Microsoft.Extensions.Logging;

    open Microsoft.AspNetCore.Builder
    open Microsoft.Extensions.DependencyInjection
    open Microsoft.AspNetCore.Identity
    open Microsoft.AspNetCore.DataProtection
    open FSharp.Control.Tasks.V2
    open Giraffe
    open Saturn
    open Shared
    open LinqToDB.Identity
    open LinqToDB.Data
    open Server.Models

    let tryGetEnv = System.Environment.GetEnvironmentVariable >> function null | "" -> None | x -> Some x

    let publicPath = Path.GetFullPath "../Client/public"

    let configuration =
        ConfigurationBuilder().AddJsonFile(
            "/home/viacheslav/repositories/famil.io/src/Server/appsettings.json").Build()

    let port = "SERVER_PORT" |> tryGetEnv |> Option.map uint16 |> Option.defaultValue 8085us

    let getInitCounter() : Task<Counter> = task { return { Value = 42 } }

    let webApp = router {
        get "/api/init" (fun next ctx ->
            task {
                let! counter = getInitCounter()
                return! json counter next ctx
            })
    }

    let configureSerialization (services:IServiceCollection) =
        services.AddSingleton<Giraffe.Serialization.Json.IJsonSerializer>(Thoth.Json.Giraffe.ThothSerializer())
        |> (fun s -> s.AddIdentity<ApplicationUser, IdentityRole>()
                     |> (fun id -> id.AddLinqToDBStores(DefaultConnectionFactory()))
                     |> (fun id -> id.AddDefaultTokenProviders()) |> ignore
                     s)
        |> (fun s -> s.AddAuthentication().AddCookie(fun o -> o.Cookie.Name <- "Interop") |> ignore
                     s) |> ignore


        DataConnection.AddConfiguration(
            "Auth",
            configuration.["ConnectionStrings:Auth"],
            PostgreSQLDataProvider(
                PostgreSQLVersion.v95))

        (use db = new DataConnection("Auth")
         try
            let sql = "create database " + "authentication"
                      //SqlConnectionStringBuilder(
                      //    configuration.["ConnectionStrings:Auth"]).InitialCatalog
            (true, db.Execute(sql) |> string)
         with e -> (false, e.Message)
        ) |> (function | (true, _ ) -> ()
                       | (false, s) -> printfn "database was not created:\n%s" s)
        (
            use db = new ApplicationDataConnection()
            Db.tryCreateTable<IdentityRole> db
            |> Option.bind Db.tryCreateTable<IdentityUserClaim<string>>
            |> Option.bind Db.tryCreateTable<IdentityRoleClaim<string>>
            |> Option.bind Db.tryCreateTable<IdentityUserLogin<string>>
            |> Option.bind Db.tryCreateTable<IdentityUserRole<string>>
            |> Option.bind Db.tryCreateTable<IdentityUserToken<string>>
        ) |> ignore
        services

    let configureApp(app: IApplicationBuilder) =
        app.UseDeveloperExceptionPage()

    let configureLogging(log: ILoggingBuilder) =
        log.AddConsole().AddDebug()
        |> ignore

    let app = application {
        url ("http://0.0.0.0:" + port.ToString() + "/")
        use_router webApp
        memory_cache
        use_config (fun _ -> configuration)
        use_static publicPath
        app_config configureApp
        logging configureLogging
        service_config configureSerialization
        use_gzip
    }

    run app
