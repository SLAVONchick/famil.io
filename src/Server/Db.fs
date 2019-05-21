namespace Server.Db

open LinqToDB.Identity
open LinqToDB
open LinqToDB.Configuration
open Server.Tables

module Db =
    [<Literal>]
    let dbName = "familio"

type ConnectionStringSettings(connStr, isGlobal, name, provider) =
    member val ConnectionString = connStr with get, set
    member val IsGlobal = isGlobal with get, set
    member val Name = name with get, set
    member val ProviderName = provider with get, set
    new(connString, name) =
        ConnectionStringSettings(connString, true, name, LinqToDB.ProviderName.PostgreSQL)
    interface IConnectionStringSettings with
        member x.ConnectionString = x.ConnectionString
        member x.IsGlobal = x.IsGlobal
        member x.Name = x.Name
        member x.ProviderName = x.ProviderName


type DbSettings(providers, conf, defaultProvider, connStrings) =
    interface ILinqToDBSettings with
        member x.DataProviders = providers
        member x.DefaultConfiguration = conf
        member x.DefaultDataProvider = defaultProvider
        member x.ConnectionStrings = connStrings

type DbFamilio() =
    inherit LinqToDB.Data.DataConnection(Db.dbName)

    member x.Roles with get() = x.GetTable<Role>()
    member x.Groups with get() = x.GetTable<Group>()
    member x.UsersRolesGroups with get() = x.GetTable<UsersRolesGroups>()
    member x.Tasks with get() = x.GetTable<Task>()
    member x.Comments with get() = x.GetTable<Comment>()