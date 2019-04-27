module Account
open Elmish
open Elmish.React

open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.PowerPack
open Fable.PowerPack.Fetch
open Shared
open Thoth.Json
open Fulma
open System.Net.Http.Headers
open Fable.PowerPack.Fetch.Fetch_types
open Elmish.Browser
open Fable.Import

let mutable isAuthorized = false

type UserStatus =
    | Authorized of User
    | NotAuthorized of exn

type State =
    | Initial
    | Loading
    | Loaded of UserStatus

type Msg =
    | StartLoading
    | LoadedData  of UserStatus
    | Reset


let getUser () : Cmd<Msg> =
    let res() = fetchAs<User> "/api/currentuser" (Decode.Auto.generateDecoder())
    let cmd =
      Cmd.ofPromise
        (res())
        []
        (Authorized >> LoadedData)
        (NotAuthorized >> LoadedData)
    cmd


let update state msg =
    match msg with
    | StartLoading ->
        let nextState = Loading
        let nextCmd = getUser()
        nextState, nextCmd
    | LoadedData u ->
        let nextState = Loaded u
        nextState, Cmd.none
    | Reset ->
        State.Initial, Cmd.none



// defines the initial state and initial command (= side-effect) of the application
let init () : State * Cmd<Msg> = State.Initial, Cmd.none

let authorizationStateView us =
  match us with
  | NotAuthorized _ ->
      isAuthorized <- false
      div [] []
  | Authorized u ->
      isAuthorized <- true
      div [] [
        div [] [
          Column.column [] [ b [ ] [ str "Nickname: " ]
                             str (u.Nickname |> Option.defaultValue "")]
        ]
        div [] [
          Column.column [] [ b [ ] [ str "Email: " ]
                             str (u.Email |> Option.defaultValue "")]
        ]
       ]


let loginButton getButton dispatch state =
    match state with
    | Initial
    | Loading ->
        getButton "#login" "Login" dispatch
    | Loaded us ->
        match us with
        | Authorized _ -> getButton "#logout" "Logout" dispatch
        | NotAuthorized _ -> getButton "#login" "Login" dispatch


let view state dispatch =
  match state with
  | Initial ->
       dispatch StartLoading
       div [] []
  | Loading ->
       div [] []
  | Loaded us ->
      authorizationStateView us