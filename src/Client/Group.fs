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
  | Loaded of Groups : Result<GroupDto * (TaskDto * string) list * User list, exn>
  | CreatingTask of Task : TaskDto * Users : User list
  | TaskCreated of Message : string
  | TaskCreationFormOpened of Task : TaskDto option * Users : User list
  | TaskEditorOpened of Task : TaskDto * Users : User list
  | EditingTask of Task : TaskDto * Users : User list
  | TaskEdited of Message : string
  | SearchUsersFormOpened of Nickname : string * Users : (User * bool) list
  | DeletingTask of GroupDto * (TaskDto * string) list * User list * TaskId : Guid
  | TaskDeleted of Result<unit, string>
  | SearchingUsers of Nickname : string * Users : (User * bool) list
  | UsersFound of Nickname : string * Users : (User * bool) list
  | UserUploading of Nickname : string * Users : (User * bool) list
  | UserUploaded of Nickname : string

type Msg =
  | StartLoading of GroupId : int64
  | LoadedData of Groups : Result<GroupDto * (TaskDto * string) list * User list, exn>
  | Reset
  | StartCreatingTask of Task : TaskDto * Users : User list
  | TaskCreatingFinished of Result : Result<Response, exn>
  | OpenTaskCreationForm of Task : TaskDto option * Users : User list
  | CloseTaskCreationForm
  | OpenTaskEditingForm of Task : TaskDto * Users : User list
  | StartEdidtingTask of Task : TaskDto * Users : User list
  | TaskEditingFinished of Result<Response, exn>
  | StartDeletingTask of Groups : (GroupDto * (TaskDto * string) list * User list) * TaskId : Guid
  | TaskDeletingFinished of Result<Response, exn>
  | OpenSearchUsersForm of Nickname : string * Users : (User * bool) list
  | StartUsersSearch of Nickname : string * GroupId : int64 * Users : (User * bool) list
  | SearchUsersFinished of Result<string * (User * bool) list, exn>
  | StartUserUploading of Nickname : string * Users : (User * bool) list * Data : UsersRolesGroupsDto
  | UserUploadingFinished of Result : Result<Response, exn>

let getGroupsByUser groupId : Cmd<Msg> =
  let res =
    fetchAs<GroupDto * (TaskDto * string) list * User list> (sprintf "/api/group/%d" groupId)
      (Decode.Auto.generateDecoder())
  let cmd = Cmd.ofPromise (res) [] (Ok >> LoadedData) (Error >> LoadedData)
  cmd

let searchUsers nick (groupId : int64) : Cmd<Msg> =
  let res =
    fetchAs<string * (User * bool) list> (sprintf "/api/group/%d/user/%s" groupId nick)
      (Decode.Auto.generateDecoder())
  Cmd.ofPromise (res) [] (Ok >> SearchUsersFinished) (Error >> SearchUsersFinished)

let postTask (task : TaskDto) =
  let res = postRecord "/api/task" task
  let cmd =
    Cmd.ofPromise (res) [ Method HttpMethod.POST ] (Ok >> TaskCreatingFinished)
      (Error >> TaskCreatingFinished)
  cmd

let putTask (task : TaskDto) =
  let res = postRecord "/api/task" task
  Cmd.ofPromise (res) [ Method HttpMethod.PUT ] (Ok >> TaskEditingFinished)
    (Error >> TaskEditingFinished)

let closeTask (taskId : Guid) =
  let res = postRecord (sprintf "/api/task/%A" taskId) null
  Cmd.ofPromise res [ Method HttpMethod.DELETE ] (Ok >> TaskDeletingFinished)
    (Error >> TaskDeletingFinished)

let postUser nick (urg : UsersRolesGroupsDto) =
  let res = postRecord (sprintf "/api/group/user/%s" nick) urg
  Cmd.ofPromise (res) [ Method HttpMethod.POST ] (Ok >> UserUploadingFinished)
    (Error >> UserUploadingFinished)

let getUserList =
  function
  | Loaded res ->
    match res with
    | Ok(_, _, us) -> us
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
  | Reset -> Initial, Cmd.none
  | StartCreatingTask(t, us) ->
    let nextState = CreatingTask(t, us)
    let nextCmd = postTask t
    nextState, nextCmd
  | TaskCreatingFinished res ->
    let message =
      match res with
      | Ok _ -> "Uploaded successfully!"
      | Error e -> e.Message

    let nextState = State.TaskCreated message
    nextState, Cmd.none
  | OpenTaskCreationForm(t, us) -> TaskCreationFormOpened(t, us), Cmd.none
  | OpenSearchUsersForm(nick, users) -> SearchUsersFormOpened(nick, users), Cmd.none
  | StartUsersSearch(nick, groupId, users) ->
    let nextState = SearchingUsers(nick, users)
    let nextCmd = searchUsers nick groupId
    nextState, nextCmd
  | SearchUsersFinished res ->
    match res with
    | Ok(nick, users) -> State.UsersFound(nick, users), Cmd.none
    | Error _ -> State.UsersFound("", []), Cmd.none
  | StartUserUploading(nick, users, urg) ->
    let nextState = UserUploading(nick, users)
    let nextCmd = postUser nick urg
    nextState, nextCmd
  | UserUploadingFinished res ->
    let nick =
      match res with
      | Ok resp -> resp.Url.Split('/') |> Seq.tryLast
      | Error _ -> None
      |> Option.defaultValue ""
    State.UserUploaded nick, Cmd.none
  | OpenTaskEditingForm(task, users) -> TaskEditorOpened(task, users), Cmd.none
  | StartEdidtingTask(task, users) -> EditingTask(task, users), putTask task
  | TaskEditingFinished res ->
    let msg =
      match res with
      | Ok _ -> "Task successfully changed!"
      | Error _ -> "An error has occured"
    TaskEdited msg, Cmd.none
  | StartDeletingTask((g, ts, us), tId) -> DeletingTask(g, ts, us, tId), closeTask tId
  | TaskDeletingFinished res ->
    let res =
      match res with
      | Ok _ -> Ok()
      | Error e -> Error(sprintf "Something went wrong!")
    TaskDeleted res, Cmd.none

// defines the initial state and initial command (= side-effect) of the application
let init() : State * Cmd<Msg> = State.Initial, Cmd.none

let taskToElement taskId dispatch onClose (task : TaskDto, userName : string) =
  Media.media []
    [ Media.content [] [ strong [] [ str (task.Name + "    ") ]
                         small [ Style [ Display "block" ] ] [ b [] [ str "Assigned to: " ]
                                                               str userName ]
                         br []
                         str (task.Description |> Option.defaultValue "No description")
                         br []
                         match task.ExpiresBy with
                         | Some dt -> sprintf "Expires by %s" (dt.ToString("MM/dd/yyyy hh:mm"))
                         | None -> ""
                         |> str ]
      Media.right [] [ Button.a [ Button.OnClick(fun _ -> dispatch task)
                                  Button.IsText ] [ str "Edit" ]
                       Button.button [ Button.OnClick(fun _ -> onClose task)
                                       Button.Color IsDanger
                                       Button.IsOutlined

                                       Button.Modifiers
                                         [ Modifier.Display(Screen.All, Display.Block) ]
                                       Button.IsLoading(taskId = task.Id) ] [ str "Close" ] ] ]

let smallButton string dispatch =
  Button.button [ Button.OnClick(fun _ -> dispatch Reset) ] [ str string ]

let rec nickNamesLink =
  function
  | [ h ] -> [ Tag.tag [] [ str (h.Nickname |> Option.defaultValue "") ] ]
  | h :: t -> [ Tag.tag [] [ str (Option.defaultValue "" h.Nickname + ", ") ] ] @ nickNamesLink t
  | _ -> [ div [] [] ]

let searchUsersModal groupId (searching : bool) nick (users : (User * bool) list)
    (dispatch : Msg -> unit) =
  let header = "Search users"

  let content =
    [ Field.div [ Field.Option.HasAddons ]
        [ Control.div [] [ Input.text [ Input.Placeholder "Type user name..."

                                        Input.OnChange
                                          (fun e -> dispatch (OpenSearchUsersForm(e.Value, users)))
                                        Input.Value nick ] ]
          Control.div [] [ Button.button [ Button.Props [ Style [ Display "block" ] ]
                                           Button.IsExpanded
                                           Button.IsFullWidth
                                           Button.Color IsInfo
                                           Button.IsLoading searching
                                           Button.OnClick(fun _ ->
                                             if not <| searching then
                                               dispatch (StartUsersSearch(nick, groupId, users))
                                             else ()) ] [ str "Seacrh" ] ] ]

      div []
        (match users with
         | [] -> [ div [] [] ]
         | _ ->
           users
           |> List.map
                (fun (u, b) ->
                Media.media []
                  [ Media.content []
                      [ strong [] [ str (Option.defaultValue "" u.Nickname) ]
                        small [] [ str (" (" + (Option.defaultValue "" u.Email) + ")") ] ]

                    Media.right []
                      [ Button.button [ Button.Color IsInfo
                                        Button.IsLoading b
                                        Button.OnClick(fun _ ->
                                          let users =
                                            users
                                            |> List.map (fun (u', _) ->
                                                 if u = u' then (u', true)
                                                 else (u', b))

                                          let urg =
                                            { UserId =
                                                u.Id
                                                |> Option.defaultWith
                                                     (fun _ -> failwith "User id is none!")
                                              RoleId = Roles.User |> int
                                              GroupId = groupId }

                                          dispatch (StartUserUploading(nick, users, urg))) ]
                          [ str "Add user" ] ] ])) ]

  let footer : Fable.Import.React.ReactElement list = []
  modal header content footer (fun _ -> dispatch Reset)

let groupToElement (g : GroupDto) (deletingTaskId : Guid) (ts : (TaskDto * string) list)
    (users : User list) dispatch =
  Tile.ancestor []
    [ Tile.parent [ Tile.Option.IsVertical
                    Tile.Option.Size Tile.ISize.Is4 ]
        [ Tile.child [ Tile.CustomClass "box"
                       Tile.Option.Modifiers [ Modifier.BackgroundColor IsDark ] ]
            [ div [] [ strong [ Style [ Color "white" ] ] [ str (g.Name) ] ]

              div []
                [ small [ Style [ Color "white" ] ]
                    [ str (g.CreatedAt.ToString "MM/dd/yyyy hh:mm") ] ] ]

          Tile.child [ Tile.Option.Modifiers [ Modifier.BackgroundColor IsGreyLight ]
                       Tile.CustomClass "box" ]
            ([ strong [] [ str "Users:" ]
               br [] ]
             @ (nickNamesLink users)
               @ [ Button.button [ Button.Props [ Style [ Display "block" ] ]
                                   Button.IsExpanded
                                   Button.OnClick(fun _ -> dispatch (OpenSearchUsersForm("", []))) ]
                     [ str "Add users" ] ]) ]

      Tile.parent []
        [ Tile.child [ Tile.Option.CustomClass "notification" ]
            ((ts
              |> List.map
                   (taskToElement deletingTaskId
                      (fun t -> OpenTaskEditingForm(t, users) |> dispatch)
                      (fun t -> dispatch (StartDeletingTask((g, ts, users), t.Id)))))
             @ [ Button.button
                   [ Button.Props [ Style [ Display "block" ] ]
                     Button.IsExpanded
                     Button.IsFullWidth
                     Button.Color IsInfo
                     Button.OnClick(fun _ -> dispatch (OpenTaskCreationForm(None, users))) ]
                   [ str "Add task" ] ]) ] ]

let userToSelectOption (user : User) =
  option [ Value(user.Id |> Option.defaultValue "") ]
    [ str (user.Nickname |> Option.defaultValue "") ]

let taskCreationModal t (us : User list) isLoading groupId userId isEdit dispatch =
  printfn "%b" isEdit
  let defaultTask t =
    Option.defaultValue { Id = Guid.NewGuid()
                          GroupId = groupId
                          Name = ""
                          Description = None
                          CreatedBy = userId |> Option.defaultValue ""
                          CreatedAt = DateTime.Now
                          Executor =
                            us
                            |> List.head
                            |> (fun u -> u.Id)
                            |> Option.defaultValue ""
                          ExpiresBy = None
                          Status = 1
                          Priority = 1 } t

  let openTaskCreationForm (t, us) =
    if isEdit then OpenTaskEditingForm(defaultTask t, us)
    else OpenTaskCreationForm(t, us)

  let startCreatingTask task us =
    if isEdit then StartEdidtingTask(task, us)
    else StartCreatingTask({ task with CreatedAt = DateTime.Now }, us)

  let task = defaultTask t
  let header = "Create task"

  let content =
    [ Media.media []
        [ Media.content []
            [ Columns.columns []
                [ Column.column [ Column.Width(Screen.All, Column.IsFull) ]
                    [ strong [] [ str "Name:" ]

                      Input.input
                        [ Input.Value task.Name

                          Input.Option.Modifiers
                            [ Modifier.Display(Screen.All, Display.Option.Block) ]
                          Input.Option.IsReadOnly isLoading

                          Input.OnChange
                            (fun e ->
                            dispatch (openTaskCreationForm (Some { task with Name = e.Value }, us))) ] ] ] ] ]

      Media.media []
        [ Media.content []
            [ Columns.columns []
                [ Column.column [ Column.Width(Screen.All, Column.IsFull) ]
                    [ strong [] [ str "Description:" ]

                      Input.input
                        [ Input.Value(task.Description |> Option.defaultValue "")

                          Input.Option.Modifiers
                            [ Modifier.Display(Screen.All, Display.Option.Block) ]
                          Input.IsReadOnly isLoading

                          Input.OnChange
                            (fun e ->
                            dispatch
                              (openTaskCreationForm
                                 (Some { task with Description = Option.fromString e.Value }, us))) ] ] ] ] ]

      Media.media []
        [ Media.content []
            [ Columns.columns []
                [ Column.column [] [ strong [] [ str "Executor:" ] ]

                  Column.column []
                    [ Select.select
                        [ Select.Disabled isLoading

                          Select.Props
                            [ OnChange
                                (fun e ->
                                dispatch
                                  (openTaskCreationForm (Some { task with Executor = e.Value }, us))) ] ]
                        [ select [] (us |> List.map (userToSelectOption)) ] ] ] ] ]

      Media.media []
        [ Media.content []
            [ Columns.columns []
                [ Column.column [ Column.Width(Screen.All, Column.IsFull) ]
                    [ strong [] [ str "Expires by:" ]

                      Flatpickr.flatpickr
                        [ Flatpickr.Disabled isLoading
                          Flatpickr.ClassName "input"
                          Flatpickr.EnableTimePicker true
                          Flatpickr.DisableBy(fun dt -> dt < DateTime.Now)
                          Flatpickr.TimeTwentyFour true

                          Flatpickr.OnChange
                            (fun dt ->
                            dispatch
                              (openTaskCreationForm (Some { task with ExpiresBy = Some dt }, us))) ] ] ] ] ] ]

  let footer =
    [ (if isLoading then
         Button.button [ Button.IsLoading isLoading
                         Button.IsFullWidth
                         Button.IsHovered true
                         Button.Color IsInfo ] []
       else
         button (if isEdit then "Save"
                 else "Create") (fun _ -> dispatch (startCreatingTask task us))) ]

  let close() = dispatch CloseTaskCreationForm
  modal header content footer close

let groupView (users : User list) (userId : string option) groupId state (dispatch : Msg -> unit) =
  match state with
  | Initial ->
    dispatch (StartLoading groupId)
    div [] []
  | Loading -> div [] []
  | Loaded res ->
    match res with
    | Error _ -> div [] []
    | Ok(g, ts, us) ->
      if g.Id = groupId then div [] [ groupToElement g (Guid()) ts us dispatch ]
      else
        dispatch (StartLoading groupId)
        div [] []
  | TaskCreationFormOpened(t, us) -> taskCreationModal t us false groupId userId false dispatch
  | CreatingTask(t, us) -> taskCreationModal (Some t) us true groupId userId false dispatch
  | TaskCreated _ ->
    dispatch Reset
    div [] []
  | TaskEditorOpened(task, us) ->
    taskCreationModal (Some task) us false groupId userId true dispatch
  | EditingTask(task, us) -> taskCreationModal (Some task) us true groupId userId true dispatch
  | TaskEdited _ ->
    dispatch Reset
    div [] []
  | DeletingTask(g, ts, us, tId) -> div [] [ groupToElement g tId ts us dispatch ]
  | TaskDeleted res ->
    match res with
    | Ok _ ->
      dispatch Reset
      div [] []
    | Error m ->
      modal "Error" [ str m ] [ button "OK" (fun _ -> dispatch Reset) ] (fun _ -> dispatch Reset)
  | SearchUsersFormOpened(nick, us) -> searchUsersModal groupId false nick us dispatch
  | SearchingUsers(nick, us) -> searchUsersModal groupId true nick us dispatch
  | UsersFound(nick, us)
  | UserUploading(nick, us) -> searchUsersModal groupId false nick us dispatch
  | UserUploaded nick ->
    dispatch (StartUsersSearch(nick, groupId, []))
    div [] []
