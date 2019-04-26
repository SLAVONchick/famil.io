namespace Shared

type Counter = { Value : int }


type User =
    { Id: string option
      Nickname: string option
      Email: string option}

type UserType =
  | Authorized of User
  | NotAuthorized