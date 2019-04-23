namespace Shared

type Counter = { Value : int }


type User =
    { Id: string
      Nickname: string
      Email: string }

type UserType =
  | Authorized of User
  | NotAuuthorized