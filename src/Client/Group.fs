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
open Elmish.Browser

type State =
    | Initial
    | Loading
    | Loaded of Groups:Result<GroupDto*(TaskDto*string) list*User list, exn>
    | UploadingTask of Task:TaskDto*Users:User list
    | TaskUploaded of Message:string
    | TaskCreationFormOpened of Task:TaskDto option*Users:User list
    | SearchUsersFormOpened of Nickname:string*Users:(User*bool) list option
    | SearchingUsers of Nickname:string*Users:(User*bool) list option
    | SearchUsersFinished of Nickname:string*Users:(User*bool) list option

type Msg =
    | StartLoading of GroupId:int64
    | LoadedData  of Groups:Result<GroupDto*(TaskDto*string) list*User list, exn>
    | Reset
    | StartUploadingTask of Task:TaskDto *Users:User list
    | TaskUploaded of Result:Result<Response, exn>
    | OpenTaskCreationForm of Task:TaskDto option*Users:User list
    | OpenSearchUsersForm of Nickname:string*Users:(User*bool) list option
    | StartUsersSearch of Nickname:string*Users:(User*bool) list option
    | CloseTaskCreationForm
    | SearchUsersFinished of Result<string*(User*bool) list option, exn>


let getGroupsByUser groupId : Cmd<Msg> =
    let res = fetchAs<GroupDto*(TaskDto*string) list*User list> (sprintf "/api/group/%d"  groupId) (Decode.Auto.generateDecoder())
    let cmd =
        Cmd.ofPromise
            (res)
            []
            (Ok >> LoadedData)
            (Error >> LoadedData)
    cmd

let searchUsers nick : Cmd<Msg> =
    let res = fetchAs<string*(User*bool) list option> (sprintf "/api/user?search=%s" nick) (Decode.Auto.generateDecoder())
    Cmd.ofPromise
        (res)
        []
        (Ok >> SearchUsersFinished)
        (Error >> SearchUsersFinished)


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
    | OpenSearchUsersForm (nick, users) ->
        SearchUsersFormOpened (nick, users), Cmd.none
    | StartUsersSearch (nick, users) ->
        let nextState = SearchingUsers (nick, users)
        let nextCmd = searchUsers nick
        nextState, nextCmd
    | SearchUsersFinished res ->
        match res with
        | Ok (nick, users) -> State.SearchUsersFinished (nick, users), Cmd.none
        | Error _ -> State.SearchUsersFinished ("", None), Cmd.none



// defines the initial state and initial command (= side-effect) of the application
let init () : State * Cmd<Msg> = State.Initial, Cmd.none

let taskToElement (task:TaskDto, userName:string) =
    Media.media [ ] [
        Media.content [  ] [
            strong [ ] [ str (task.Name + "    ") ]
            small [ Style [ Display "block" ] ] [
                b [] [ str "Assigned to: " ]
                str userName ]
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
let rec nickNamesLink =
    function
    | [h]   -> [ Tag.tag [] [ str (h.Nickname |> Option.defaultValue "") ] ]
    | h::t  -> [ Tag.tag [] [ str (Option.defaultValue "" h.Nickname + ", ") ] ] @ nickNamesLink t
    | _     -> [ div [] [] ]

let searchUsersModal (searching: bool) nick (users: (User*bool) list) (dispatch: Msg -> unit) =
    let header = "Search users"
    let content = [
        Field.div
            [Field.Option.HasAddons]
            [
                Control.div [] [
                    Input.text [
                        Input.Placeholder "Type user name..."
                        Input.OnChange (fun e -> dispatch (OpenSearchUsersForm (e.Value, users |> (fun u ->
                            match u with [] -> None | _ -> Some u )) ) )
                        Input.Value nick
                    ]
                ]
                Control.div [] [
                    Button.button [
                        Button.Props [ Style [ Display "block" ] ]
                        Button.IsExpanded
                        Button.IsFullWidth
                        Button.Color IsInfo
                        Button.IsLoading searching
                        Button.OnClick (fun _ ->
                            if searching |> not then
                                dispatch (StartUsersSearch (nick, users |> (fun u ->
                                    match u with [] -> None | _ -> Some u ) ) )
                            else
                                () ) ] [
                            str "Seacrh"
                    ]
                ]
            ]
        (match users with
        | [] -> div [] []
        | _ ->
            Select.select [] [
                select [] (
                    users
                    |> List.fold (fun el (u,b) -> el @ [
                        option [
                            OnSelect (fun _ ->
                                dispatch (OpenSearchUsersForm (nick,
                                                        users
                                                        |> List.map (fun (u',b') ->
                                                            if u = u' then (u, not b') else (u', b') )
                                                        |> Some
                                                        )
                                        )
                                    )
                            Value ((Option.defaultValue "" u.Nickname) + " (" + (Option.defaultValue "" u.Email) + ")" )
                        ] []
                    ]
                ) []
            )
            ])
    ]
    let footer : Fable.Import.React.ReactElement list = []
    modal header content footer (fun _ -> dispatch Reset)

let groupToElement
    (g:GroupDto)
    (ts:(TaskDto*string) list)
    (users: User list)
    dispatch =

    Tile.ancestor [] [
        Tile.parent [
            Tile.Option.IsVertical
            Tile.Option.Size Tile.ISize.Is4
        ] [
            Tile.child [
                Tile.CustomClass "box"
                Tile.Option.Modifiers [ Modifier.BackgroundColor IsDark ] ] [
                    div [ ] [ strong [ Style [ CSSProp.Color "white" ] ] [ str (g.Name) ] ]
                    div [] [ small [ Style [ CSSProp.Color "white" ] ] [
                        str (g.CreatedAt.ToString "MM/dd/yyyy hh:mm") ] ]
            ]
            Tile.child [
                Tile.Option.Modifiers [ Modifier.BackgroundColor IsGreyLight ]
                Tile.CustomClass "box" ]
                ([ strong [] [ str "Users:" ]
                   br [] ] @
                (nickNamesLink users) @
                 [ Button.button [
                    Button.Props [ Style [ Display "block" ] ]
                    Button.IsExpanded
                    Button.OnClick (fun _ -> dispatch (OpenSearchUsersForm ("", None) ) )
                 ] [
                    str "Add users"
                   ]
                ]  )
            ]
        Tile.parent [
        ] [
            Tile.child [
                Tile.Option.CustomClass "notification"
            ] ((ts |> List.map taskToElement)@ [
                Button.button [
                    Button.Props [ Style [ Display "block" ] ]
                    Button.IsExpanded
                    Button.IsFullWidth
                    Button.Color IsInfo
                    Button.OnClick (fun _ -> dispatch (OpenTaskCreationForm (None, users)))
          ] [
            str "Add task"
            ]
            ] )
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
                            Select.select [ Select.Disabled isLoading ] [
                                select [] (
                                    us
                                    |> List.map (userToSelectOption (fun id ->
                                        dispatch (OpenTaskCreationForm (Some {task with Executor = id}, us) )
                                        ) )
                                    )
                            ]

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
                  Button.IsFullWidth
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
            then
                div [] [ groupToElement g ts us dispatch ]
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
    | SearchUsersFormOpened (nick, us) ->
        searchUsersModal false nick (Option.defaultValue [] us) dispatch
    | SearchingUsers (nick, us) ->
        searchUsersModal true nick (Option.defaultValue [] us) dispatch
    | State.SearchUsersFinished (nick, us) ->
        searchUsersModal false nick (Option.defaultValue [] us) dispatch