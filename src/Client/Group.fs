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
open System

type State =
    | Initial
    | Loading
    | Loaded of Groups:Result<GroupDto*(TaskDto*string) list*User list, exn>
    | UploadingTask of Task:TaskDto*Users:User list
    | TaskUploaded of Message:string
    | TaskCreationFormOpened of Task:TaskDto option*Users:User list

type Msg =
    | StartLoading of GroupId:int64
    | LoadedData  of Groups:Result<GroupDto*(TaskDto*string) list*User list, exn>
    | Reset
    | StartUploadingTask of Task:TaskDto *Users:User list
    | TaskUploaded of Result<Response, exn>
    | OpenTaskCreationForm of Task:TaskDto option*Users:User list
    | CloseTaskCreationForm


let getGroupsByUser groupId : Cmd<Msg> =
    let res = fetchAs<GroupDto*(TaskDto*string) list*User list> (sprintf "/api/group/%d"  groupId) (Decode.Auto.generateDecoder())
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
    | StartUploadingTask (t, us) ->
        let nextState = UploadingTask (t, us)
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

let taskToElement (task:TaskDto, userName:string) =
    Media.media [ ] [
        Media.content [  ] [
            strong [ ] [ str (task.Name + "    ") ]
            small [ ] [ str userName ]
            br [ ]
            str (task.Description |> Option.defaultValue "No description")
            br [ ]
            match task.ExpiresBy with
            | Some dt -> sprintf "Expires by %s" (dt.ToString("MM/dd/yyyy hh:mm"))
            | None -> ""
            |> str
        ]
    ]

let smallButton string dispatch =
    Button.button [
        Button.OnClick (fun _ -> dispatch Reset)
    ] [
        str string
    ]

let groupToElement (g:GroupDto) (ts:(TaskDto*string) list) users dispatch =
    let addFriendLink = ""
    Tile.ancestor [] [
        Tile.parent [
            Tile.Option.IsVertical
            Tile.Option.Size Tile.ISize.Is5
        ] [
            Tile.child [ Tile.CustomClass "box" ] [
                div [] [ strong [] [ str (g.Name) ] ]
                div [] [ small [] [ str (g.CreatedAt.ToString "MM/dd/yyyy hh:mm") ] ]
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
        ] [
            Tile.child [] ((ts |> List.map taskToElement))
            Tile.child [] [
                Button.button [
                Button.IsExpanded
                Button.IsFullWidth
                Button.OnClick (fun _ -> dispatch (OpenTaskCreationForm (None, users)))
          ] [
            str "Add task"
            ]
            ]
        ]
    ]

let userToSelectOption dispatch (user:User) =
    option [
        OnSelect (fun _ -> dispatch (user.Id |> Option.defaultValue ""))
        Value (user.Id |> Option.defaultValue "") ] [ str (user.Nickname |> Option.defaultValue "") ]

let taskCreationModal t (us:User list) isLoading groupId userId dispatch =
    let task =
        Option.defaultValue
            { Id = System.Guid.NewGuid()
              GroupId = groupId
              Name = ""
              Description = None
              CreatedBy = userId |> Option.defaultValue ""
              CreatedAt = System.DateTime.Now
              Executor = us |> List.head |> (fun u -> u.Id) |> Option.defaultValue ""
              ExpiresBy = None
              Status = 1
              Priority = 1 }
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
                                Input.Option.IsReadOnly isLoading
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
                                Input.IsReadOnly isLoading
                                Input.OnChange (fun e ->
                                    dispatch (OpenTaskCreationForm (Some {task with Description = Option.fromString e.Value}, us ) ) )
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
                            Select.select [ Select.Disabled isLoading ]
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
                            Flatpickr.flatpickr [
                                Flatpickr.Disabled isLoading
                                Flatpickr.ClassName "input"
                                Flatpickr.EnableTimePicker true
                                Flatpickr.DisableBy (fun dt -> dt < DateTime.Now)
                                Flatpickr.TimeTwentyFour true
                                Flatpickr.OnChange (fun dt ->
                                    dispatch (OpenTaskCreationForm (Some {task with ExpiresBy = Some dt}, us) ) )
                            ]
                        ]
                    ]
                ]
            ]
         ]
    let footer =
          [
              (if isLoading
              then Button.button [
                  Button.IsLoading isLoading
                  Button.IsHovered true
                  Button.Color IsInfo ] []
              else button "Create" (fun _ -> dispatch (StartUploadingTask ({task with CreatedAt = System.DateTime.Now}, us) ) ) )
          ]
    let close() = dispatch CloseTaskCreationForm
    modal header content footer close

let groupView (users:User list) (userId:string option) groupId state (dispatch: Msg -> unit) =
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
      taskCreationModal t us false groupId userId dispatch
  | UploadingTask (t, us) ->
      taskCreationModal (Some t) us true groupId userId dispatch
  | State.TaskUploaded _ ->
      dispatch Reset
      div [] []