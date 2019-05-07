namespace Server.Tables
open System
open LinqToDB.Mapping
open Shared


[<Table(Name="familio.roles")>]
[<CLIMutable>]
type Role =
    {
        [<PrimaryKey>]
        [<Identity>]
        [<Column(Name="id")>]
        Id:int

        [<NotNull>]
        [<Column(Name="name")>]
        Name:string
    }

[<Table(Name="familio.groups")>]
[<CLIMutable>]
type Group =
    {
        [<PrimaryKey>]
        [<Identity>]
        [<Column(Name="id")>]
        Id: int64

        [<NotNull>]
        [<Column(Name="name")>]
        Name: string

        [<NotNull>]
        [<Column(Name="created_by")>]
        CreatedBy: string

        [<NotNull>]
        [<Column(Name="created_at")>]
        CreatedAt: DateTime

        [<Column(Name="deleted_by")>]
        DeletedBy: string option

        [<Column(Name="deleted_at")>]
        DeletedAt: DateTime option
    }

[<Table(Name="familio.users_roles_groups")>]
[<CLIMutable>]
type UsersRolesGroups =
    {
        [<NotNull>]
        [<Column(Name="user_id")>]
        UserId: string

        [<NotNull>]
        [<Column(Name="role_id")>]
        RoleId: int

        [<Association(Storage="roles",ThisKey="role_id",OtherKey="id")>]
        Role: Role

        [<NotNull>]
        [<Column(Name="group_id")>]
        GroupId: int64

        [<Association(Storage="groups",ThisKey="group_id",OtherKey="id")>]
        Group: Group
    }

[<Table(Name="familio.tasks")>]
[<CLIMutable>]
type Task =
    {
        [<PrimaryKey>]
        [<Identity>]
        [<Column(Name="id")>]
        Id:Guid

        [<NotNull>]
        [<Column(Name="group_id")>]
        GroupId: int64

        [<Association(Storage="groups",ThisKey="group_id",OtherKey="id")>]
        Group: Group

        [<NotNull>]
        [<Column(Name="name")>]
        Name: string

        [<Column(Name="description")>]
        Description: string option

        [<NotNull>]
        [<Column(Name="created_by")>]
        CreatedBy: string

        [<NotNull>]
        [<Column(Name="created_at")>]
        CreatedAt: DateTime

        [<NotNull>]
        [<Column(Name="executor")>]
        Executor: string

        [<Column(Name="expires_by")>]
        ExpiresBy: DateTime option

        [<NotNull>]
        [<Column(Name="status")>]
        Status: Status

        [<NotNull>]
        [<Column(Name="priority")>]
        Priority: Priority
    }

[<Table(Name="familio.comments")>]
[<CLIMutable>]
type Comment =
    {
        [<NotNull>]
        [<Column(Name="id",IsPrimaryKey=true)>]
        Id: Guid

        [<NotNull>]
        [<Column(Name="task_id",IsPrimaryKey=true)>]
        TaskId: Guid

        [<NotNull>]
        [<Column(Name="user_id",IsPrimaryKey=true)>]
        UserId: string

        [<NotNull>]
        [<Column(Name="contents")>]
        Contents: string

        [<NotNull>]
        [<Column(Name="created_at")>]
        CreatedAt: DateTime

        [<Column(Name="updated_at")>]
        UpdatedAt: DateTime option
    }