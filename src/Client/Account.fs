module Account

open Elmish
open Fable.Helpers.React
open Fable.PowerPack.Fetch
open Shared
open Thoth.Json
open Fulma
open Elmish.Browser

type UserStatus =
  | Authorized of User
  | NotAuthorized of exn

type State =
  | Initial
  | Loading
  | Loaded of UserStatus

type Msg =
  | StartLoading
  | LoadedData of UserStatus
  | Reset

let getUser() : Cmd<Msg> =
  let res() = fetchAs<User> "/api/users/current" (Decode.Auto.generateDecoder())
  let cmd = Cmd.ofPromise (res()) [] (Authorized >> LoadedData) (NotAuthorized >> LoadedData)
  cmd

let authWith (state : State) whenAuth whenNotAuth =
  match state with
  | Loaded us -> whenAuth us
  | _ -> whenNotAuth

let isAuthorized (state : State) =
  authWith state (fun us ->
    match us with
    | Authorized _ -> true
    | _ -> false) false

let getUserId (state : State) =
  let whenNotAuth = None
  authWith state (fun us ->
    match us with
    | Authorized u -> u.Id
    | _ -> whenNotAuth) whenNotAuth

let update state msg =
  match msg with
  | StartLoading ->
    let nextState = Loading
    let nextCmd = getUser()
    nextState, nextCmd
  | LoadedData u ->
    let nextState = Loaded u
    nextState, Cmd.none
  | Reset -> State.Initial, Cmd.none

let init() : State * Cmd<Msg> = Initial, Cmd.none

let authorizationStateView us =
  match us with
  | NotAuthorized _ -> div [] []
  | Authorized u ->
    div [] [ div [] [ Column.column [] [ b [] [ str "Nickname: " ]
                                         str (u.Nickname |> Option.defaultValue "") ] ]
             div [] [ Column.column [] [ b [] [ str "Email: " ]
                                         str (u.Email |> Option.defaultValue "") ] ] ]

let loginButton getButton dispatch state =
  match state with
  | Initial
  | Loading -> getButton "#login" "Login" dispatch
  | Loaded us ->
    match us with
    | Authorized _ -> getButton "#logout" "Logout" dispatch
    | NotAuthorized _ -> getButton "#login" "Login" dispatch

let view state dispatch =
  match state with
  | Initial -> div [] []
  | Loading -> div [] []
  | Loaded us -> authorizationStateView us
