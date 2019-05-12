module Client.Group

open Elmish
open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.PowerPack.Fetch
open Thoth.Json
open Fulma
open Shared.Dto
open Client.Common

type State =
    | Initial
    | Loading
    | Loaded of Groups:Result<GroupDto*Task list, exn>

type Msg =
    | StartLoading of GroupId:int64
    | LoadedData  of Groups:Result<GroupDto*Task list, exn>
    | Reset


let getGroupsByUser groupId : Cmd<Msg> =
    let res = fetchAs<GroupDto*Task list> (sprintf "/api/group/%d"  groupId) (Decode.Auto.generateDecoder())
    let cmd =
      Cmd.ofPromise
        (res)
        []
        (Ok >> LoadedData)
        (Error >> LoadedData)
    cmd

let update state msg =
    match msg with
    | StartLoading uid ->
        let nextState = Loading
        let nextCmd = getGroupsByUser uid
        nextState, nextCmd
    | LoadedData u ->
        let nextState = Loaded u
        nextState, Cmd.none
    | Reset ->
        Initial, Cmd.none



// defines the initial state and initial command (= side-effect) of the application
let init () : State * Cmd<Msg> = State.Initial, Cmd.none

let taskToElement (task:Task) =
    Media.media [] [
        Media.content [] [
            Content.content [] [
                Hero.hero [ Hero.CustomClass "is-dark" ] [
                    Hero.body [] [
                        Content.content [] [
                            table [] [
                                th [] []
                                th [] []
                                tr [] [
                                    td [] [
                                        title [] [ str task.Name ]
                                    ]
                                    td [] [
                                        small [] [ str (task.CreatedAt.ToString "dd.MM.yyyy hh:mm") ]
                                    ]
                                ]
                                tr [ Span 2. ] [
                                    p [] [ str (task.Description |> Option.defaultValue "No description") ]
                                ]
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]

let smallButton string dispatch =
    Button.button [
        Button.OnClick (fun _ -> dispatch Reset)
    ] [
        str string
    ]

let groupToElement (g:GroupDto) (ts:Task list) dispatch =
    let addFriendLink = ""
    Tile.ancestor [] [
        Tile.parent [
            Tile.Option.IsVertical
            Tile.Option.Size Tile.ISize.Is4
        ] [
            Tile.child [ Tile.CustomClass "box" ] [
                div [] [ strong [] [ str (g.Name) ] ]
                div [] [ small [] [ str (g.CreatedAt.ToString "dd.MM.yyyy hh:mm") ] ]
            ]
            Tile.child [ Tile.CustomClass "box" ] [
                div [] [
                    a [ Href addFriendLink ] [ str addFriendLink ]
                    smallButton "Reset" dispatch
                ]
            ]
        ]
        Tile.parent [] (ts |> List.map taskToElement)
    ]

let groupsView groupId state (dispatch: Msg -> unit) =
  match state with
  | Initial ->
       dispatch (StartLoading groupId)
       div [] []
  | Loading ->
       div [] []
  | Loaded res ->
      match res with
      | Error _ ->
          div [] []
      | Ok (g, ts) ->
          div [] [ groupToElement g ts dispatch ]