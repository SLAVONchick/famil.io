namespace Shared

open System

module Functions =
  let inline (|IsBigEnough|_|) len (input : ^a) =
    let inputLen = (^a : (member Length : int) input)
    if inputLen >= len then Some()
    else None

  let prepare (nick : string) =
    match nick with
    | IsBigEnough 3 -> "*" + nick + "*"
    | _ -> nick + "*"

type Status =
  | Created = 1
  | Closed = 2

type Priority =
  | Highest = 1

type User =
  { Id : string option
    Nickname : string option
    Email : string option }

type Roles =
  | User = 1
  | Admin = 2

module Dto =
  type RoleDto =
    { Id : int
      Name : string }

  type GroupDto =
    { Id : int64
      Name : string
      CreatedBy : string
      CreatedAt : DateTime
      DeletedBy : string option
      DeletedAt : DateTime option }

  type UsersRolesGroupsDto =
    { UserId : string
      RoleId : int
      GroupId : int64 }

  type TaskDto =
    { Id : Guid
      GroupId : int64
      Name : string
      Description : string option
      CreatedBy : string
      CreatedAt : DateTime
      Executor : string
      ExpiresBy : DateTime option
      Status : int
      Priority : int }

  type CommentDto =
    { Id : Guid
      TaskId : Guid
      UserId : string
      Contents : string
      CreatedAt : DateTime
      UpdatedAt : DateTime option }

module Option =
  let fromString =
    function
    | null
    | "" -> None
    | x -> Some x
