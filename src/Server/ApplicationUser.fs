namespace  Server.Models

open LinqToDB.Identity

type ApplicationUser = class
    inherit IdentityUser
    end