module Groups

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
    | Loaded of Groups:Result<GroupDto list, exn>
    | UploadingGroup
    | GroupUploaded of Message:string
    | GroupCreationFormOpened of GroupName:string option

type Msg =
    | StartLoading of UserId:string option
    | LoadedData  of Groups:Result<GroupDto list, exn>
    | Reset
    | StartUploadingGroup of Group:GroupDto
    | GroupsUploaded of Result<Response, exn>
    | OpenGroupCreationForm of GroupName:string option
    | CloseGroupCreationForm


let getGroupsByUser userId : Cmd<Msg> =
    let uid = userId |> Option.defaultValue ""
    let res = fetchAs<GroupDto list> ("/api/groups/" + uid) (Decode.Auto.generateDecoder())
    let cmd =
      Cmd.ofPromise
        (res)
        []
        (Ok >> LoadedData)
        (Error >> LoadedData)
    cmd

let postGroup (group:GroupDto) =
    let res = postRecord "/api/group" group
    let cmd =
        Cmd.ofPromise
            (res)
            [ Method HttpMethod.POST ]
            (Ok >> GroupsUploaded)
            (Error >> GroupsUploaded)
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
    | StartUploadingGroup g ->
        let nextState = UploadingGroup
        let nextCmd = postGroup g
        nextState, nextCmd
    | GroupsUploaded res ->
        let message =
            match res with
            | Ok _ -> "Uploaded successfully!"
            | Error e -> e.Message
        let nextState = GroupUploaded message
        nextState, Cmd.none
    | OpenGroupCreationForm name ->
        GroupCreationFormOpened name, Cmd.none
    | Reset
    | CloseGroupCreationForm ->
        Initial, Cmd.none



// defines the initial state and initial command (= side-effect) of the application
let init () : State * Cmd<Msg> = State.Initial, Cmd.none

let groupToElement navigateTo (g:GroupDto) =
    Media.media [] [
        Media.content [] [
            Content.content [] [
                div [] [ strong [] [ a [ OnClick (fun _ -> navigateTo <| sprintf "#group/%d" g.Id ) ] [ str g.Name ] ] ]
                div [] [ small [] [ str <| sprintf "Created %s" (g.CreatedAt.ToString("dd.MM.yyyy hh:mm")) ] ]
            ]
        ]
    ]

let groupsView userId state (dispatch: Msg -> unit) navigateTo =
  match state with
  | Initial ->
       dispatch (StartLoading userId)
       div [] []
  | Loading ->
       div [] []
  | Loaded res ->
      match res with
      | Error _ ->
          div [] []
      | Ok gl ->
          div [] ((gl
              |> List.map (groupToElement navigateTo))
              @ [ button "Add" (fun _ -> dispatch (OpenGroupCreationForm None)) ] )
  | UploadingGroup ->
      div [] []
  | GroupUploaded msg ->
      dispatch Reset
      div [] []
  | GroupCreationFormOpened name ->
      let content =
          [
           div [] [ b [] [ str "Name:" ] ]
           div [] [ input [ Value (name |> function None -> "" | Some s -> s)
                            OnChange (fun e ->
                                        let newName =
                                            match e.Value with
                                            | name when name |> System.String.IsNullOrEmpty -> None
                                            | name -> Some name
                                        dispatch (OpenGroupCreationForm newName) )
                            ] ] ]
      let header = "Create group"
      let footer =
          [ button "Create" (fun _ ->
                            let group =
                                { Id = 0L
                                  Name = name |> Option.defaultValue ""
                                  CreatedAt = System.DateTime.Now.ToUniversalTime()
                                  CreatedBy = userId |> Option.defaultValue ""
                                  DeletedAt = None
                                  DeletedBy = None }
                            dispatch (StartUploadingGroup group))
                    ]
      let close () = CloseGroupCreationForm |> dispatch
      modal header content footer close