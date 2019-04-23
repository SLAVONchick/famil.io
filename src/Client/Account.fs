module Account
open Elmish
open Elmish.React

open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.PowerPack.Fetch
open Shared
open Thoth.Json
open Fulma

type Model =
  { User: User option }

type Msg =
  | Authorized of User
  | NotAuthorized of exn

let getUser() = fetchAs<User> "/api/currentuser" (Decode.Auto.generateDecoder())

// defines the initial state and initial command (= side-effect) of the application
let init () : Model * Cmd<Msg> =
    let initialModel = { User = None }
    let loadCountCmd =
        Cmd.ofPromise
            (getUser ())
            []
            Authorized
            NotAuthorized
    initialModel, loadCountCmd

let view() =
  let (model, _) = init()
  match model.User with
  | None -> []
  | Some u ->
    [
          Column.column [] [
              div [] [ b [] [ str "Nickname:" ] ]
              div [] [ b [] [ str "Email:" ] ]
          ]
          Column.column [] [
              div [] [ b [] [ str u.Nickname ] ]
              div [] [ b [] [ str u.Email ] ]
        ]
    ]