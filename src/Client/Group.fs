module Client.Group

open Elmish
open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.PowerPack.Fetch
open Thoth.Json
open Fulma
open Shared.Dto
open Shared
open Client.Common

type State =
    | Initial
    | Loading
    | Loaded of Groups:Result<GroupDto*TaskDto list*User list, exn>
    | UploadingTask
    | TaskUploaded of Message:string
    | TaskCreationFormOpened of Task:TaskDto option*Users:User list

type Msg =
    | StartLoading of GroupId:int64
    | LoadedData  of Groups:Result<GroupDto*TaskDto list*User list, exn>
    | Reset
    | StartUploadingTask of Task:TaskDto
    | TaskUploaded of Result<Response, exn>
    | OpenTaskCreationForm of Task:TaskDto option*Users:User list
    | CloseTaskCreationForm


let getGroupsByUser groupId : Cmd<Msg> =
    let res = fetchAs<GroupDto*TaskDto list*User list> (sprintf "/api/group/%d"  groupId) (Decode.Auto.generateDecoder())
    let cmd =
      Cmd.ofPromise
        (res)
        []
        (Ok >> LoadedData)
        (Error >> LoadedData)
    cmd


let postTask (task:TaskDto) =
    let res = postRecord "/api/task" task
    let cmd =
        Cmd.ofPromise
            (res)
            [ Method HttpMethod.POST ]
            (Ok >> TaskUploaded)
            (Error >> TaskUploaded)
    cmd

let getUserList =
    function
    | Loaded res ->
        match res with
        | Ok (_, _, us) -> us
        | _ -> []
    | _ -> []



let update state msg =
    match msg with
    | StartLoading uid ->
        let nextState = Loading
        let nextCmd = getGroupsByUser uid
        nextState, nextCmd
    | LoadedData u ->
        let nextState = Loaded u
        nextState, Cmd.none
    | CloseTaskCreationForm
    | Reset ->
        Initial, Cmd.none
    | StartUploadingTask t ->
        let nextState = UploadingTask
        let nextCmd = postTask t
        nextState, nextCmd
    | TaskUploaded res ->
        let message =
            match res with
            | Ok _ -> "Uploaded successfully!"
            | Error e -> e.Message
        let nextState = State.TaskUploaded message
        nextState, Cmd.none
    | OpenTaskCreationForm (t, us) ->
        TaskCreationFormOpened (t, us), Cmd.none



// defines the initial state and initial command (= side-effect) of the application
let init () : State * Cmd<Msg> = State.Initial, Cmd.none

let taskToElement (task:TaskDto) =
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

let groupToElement (g:GroupDto) (ts:TaskDto list) users dispatch =
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
        Tile.parent [
            Tile.Option.CustomClass "is-fullwidth"
        ] ((ts |> List.map taskToElement)@ [
            Button.button [
            Button.IsFullWidth
            Button.OnClick (fun _ -> dispatch (OpenTaskCreationForm (None, users)))
          ] [
            str "Add"
            ] ])
    ]

let userToSelectOption dispatch (user:User) =
    option [
        OnSelect (fun _ -> dispatch (user.Id |> Option.defaultValue ""))
        Value (user.Id |> Option.defaultValue "") ] [ str (user.Nickname |> Option.defaultValue "") ]

let groupsView (users:User list) (userId:string option) groupId state (dispatch: Msg -> unit) =
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
      | Ok (g, ts, us) ->
          if g.Id = groupId
          then div [] [ groupToElement g ts us dispatch ]
          else
            dispatch (StartLoading groupId)
            div [] []
  | TaskCreationFormOpened (t, us) ->
      let task =
        Option.defaultValue
            { Id = System.Guid()
              GroupId = groupId
              Name = ""
              Description = None
              CreatedBy = userId |> Option.defaultValue ""
              CreatedAt = System.DateTime.Now
              Executor = ""
              ExpiresBy = None
              Status = Status.Created
              Priority = Priority.Highest }
            t
      let header = "Create task"
      let content =
        [
            Media.media [] [
                Media.content [] [
                    Columns.columns [] [
                        Column.column [ Column.Width (Screen.All, Column.IsFull) ] [
                            strong [ ] [ str "Name:" ]
                            Input.input [
                                Input.Value task.Name
                                Input.Option.Modifiers [ Modifier.Display (Screen.All, Display.Option.Block) ]
                                Input.OnChange (fun e ->
                                    dispatch (OpenTaskCreationForm (Some {task with Name = e.Value}, us ) ) )
                            ]
                        ]
                    ]
                ]
            ]
            Media.media [] [
                Media.content [] [
                    Columns.columns [] [
                        Column.column [ Column.Width (Screen.All, Column.IsFull) ] [
                            strong [ ] [ str "Description:" ]
                            Input.input [
                                Input.Value (task.Description |> Option.defaultValue "")
                                Input.Option.Modifiers [ Modifier.Display (Screen.All, Display.Option.Block) ]
                                Input.OnChange (fun e ->
                                    dispatch (OpenTaskCreationForm (Some {task with Description = Option.fromObj e.Value}, us ) ) )
                            ]
                        ]
                    ]
                ]
            ]
            Media.media [] [
                Media.content [] [
                    Columns.columns [] [
                        Column.column [  ] [
                            strong [ ] [ str "Executor:" ]
                        ]
                        Column.column [  ] [
                            Select.select [ ]
                                (us
                                |> List.map (userToSelectOption (fun id ->
                                    dispatch (OpenTaskCreationForm (Some {task with Executor = id}, us) )
                                    ) ) )
                        ]
                    ]
                ]
            ]
            Media.media [] [
                Media.content [] [
                    Columns.columns [] [
                        Column.column [ Column.Width (Screen.All, Column.IsFull) ] [
                            strong [ ] [ str "Expires by:" ]
                            Input.week []
                        ]
                    ]
                ]
            ]
         ]
      let footer =
          [
              button "Create" (fun _ -> dispatch (StartUploadingTask {task with CreatedAt = System.DateTime.Now}))
          ]
      let close() = dispatch CloseTaskCreationForm
      modal header content footer close
    | State.TaskUploaded _ ->
        div [] []
    | UploadingTask ->
        div [] []