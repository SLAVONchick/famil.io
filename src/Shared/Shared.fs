namespace Shared
open System

type Counter = { Value : int }

type Status = Created = 1

type Priority = Highest = 1

type User =
    { Id: string option
      Nickname: string option
      Email: string option }

type Roles =
    | User = 1
    | Admin = 2

module Dto =
  type RoleDto =
      { Id:int
        Name:string }
  type GroupDto =
      { Id: int64
        Name: string
        CreatedBy: string
        CreatedAt: DateTime
        DeletedBy: string option
        DeletedAt: DateTime option }
  type UsersRolesGroupsDto =
      { UserId: string
        RoleId: int
        GroupId: int64
        Group: GroupDto }
  type TaskDto =
      { Id:Guid
        GroupId: int64
        Name: string
        Description: string option
        CreatedBy: string
        CreatedAt: DateTime
        Executor: string
        ExpiresBy: DateTime option
        Status: Status
        Priority: Priority }
  type CommentDto =
      { Id: Guid
        TaskId: Guid
        UserId: string
        Contents: string
        CreatedAt: DateTime
        UpdatedAt: DateTime option }

module Option =
    let fromObj =
        function null | "" -> None | x -> Some x