namespace Server

module Db =
  open LinqToDB.Identity
  open LinqToDB
  open LinqToDB.Configuration
  open Server.Tables

  module Db =
    [<Literal>]
    let DbName = "familio"

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
    inherit LinqToDB.Data.DataConnection(Db.DbName)
    member x.Roles = x.GetTable<Role>()
    member x.Groups = x.GetTable<Group>()
    member x.UsersRolesGroups = x.GetTable<UsersRolesGroups>()
    member x.Tasks = x.GetTable<Task>()
    member x.Comments = x.GetTable<Comment>()
