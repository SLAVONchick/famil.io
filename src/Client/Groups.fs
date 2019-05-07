module Groups

open Elmish
open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.PowerPack.Fetch
open Fable.PowerPack.Fetch.Fetch_types
open Thoth.Json
open Fulma
open Shared.Dto

type State =
    | Initial
    | Loading
    | Loaded of Groups:Result<Group list, exn>
    | UploadingGroup
    | GroupUploaded of Message:string
    | GroupCreationFormOpened of GroupName:string option

type Msg =
    | StartLoading of UserId:string
    | LoadedData  of Groups:Result<Group list, exn>
    | Reset
    | StartUploadingGroup of Group:Group
    | GroupsUploaded of Result<Response, exn>
    | OpenGroupCreationForm of GroupName:string option
    | CloseGroupCreationForm


let getGroupsByUser userId : Cmd<Msg> =
    let res = fetchAs<Group list> ("/api/groups/" + userId) (Decode.Auto.generateDecoder())
    let cmd =
      Cmd.ofPromise
        (res)
        []
        (Ok >> LoadedData)
        (Error >> LoadedData)
    cmd

let postGroup (group:Group) =
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

let groupToElement (g:Group) =
    Media.media [] [
        Media.content [] [
            div [] [ strong [] [ str g.Name ] ]
            div [] [ strong [] [ str (g.CreatedAt.ToString()) ] ]
        ]
    ]



let getterView userId state dispatch =
  match state with
  | Initial ->
       dispatch (StartLoading userId)
       [ div [] [] ]
  | Loading ->
       [ div [] [] ]
  | Loaded res ->
      match res with
      | Error _ ->
          [ div [] [] ]
      | Ok gl ->
          gl
          |> List.map groupToElement
          |> List.append [
              button [
                  OnClick (fun _ -> dispatch (OpenGroupCreationForm None))
                  Class Button.Classes.List.IsCentered
              ] [ str "Add" ]
          ]
  | UploadingGroup ->
      []
  | GroupUploaded msg ->
      []
  | GroupCreationFormOpened name ->
      [ Modal.modal
            [  ]
            [
                div [] [ b [] [ str "Name:" ] ]
                div [] [ input [
                    Value (name |> function None -> "" | Some s -> s)
                    OnChange (fun e ->
                        let newName =
                            match e.Value with
                            | name when name |> System.String.IsNullOrEmpty -> None
                            | name -> Some name
                        dispatch (OpenGroupCreationForm newName) )
                ] ]
            ] ]