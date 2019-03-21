namespace Server.Models

open LinqToDB.Identity
open LinqToDB
type ApplicationDataConnection() = class
    inherit IdentityDataConnection<ApplicationUser>()
    end


module Db =
    let tryCreateTable<'T> (db: ApplicationDataConnection) =
        try
            db.CreateTable<'T>() |> ignore
            Some db
        with _ -> None