namespace Server.Tables
open System
open LinqToDB.Mapping
open Shared
open System.ComponentModel.DataAnnotations
open LinqToDB

module Const =
    let dataType =
        DataType.DateTime


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

        [<NotNull>]
        [<Column(Name="group_id")>]
        GroupId: int64
    }

[<Table(Name="familio.tasks")>]
[<CLIMutable>]
type Task =
    {
        [<PrimaryKey>]
        [<Column(Name="id")>]
        Id:Guid

        [<NotNull>]
        [<Column(Name="group_id")>]
        GroupId: int64

        [<NotNull>]
        [<Column(Name="name")>]
        Name: string

        [<Column(Name="description", CanBeNull=true, DataType=DataType.Text)>]
        Description: string

        [<NotNull>]
        [<Column(Name="created_by")>]
        CreatedBy: string

        [<NotNull>]
        [<Column(Name="created_at")>]
        CreatedAt: DateTime

        [<NotNull>]
        [<Column(Name="executor")>]
        Executor: string

        [<Column(Name="expires_by", CanBeNull=true, DataType=DataType.DateTime)>]
        ExpiresBy: Nullable<DateTime>

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