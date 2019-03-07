module Client

open Elmish
open Elmish.React

open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.PowerPack.Fetch

open Thoth.Json

open Shared


open Fulma
open Fable.Helpers


// The model holds data that you want to keep track of while the application is running
// in this case, we are keeping track of a counter
// we mark it as optional, because initially it will not be available from the client
// the initial value will be requested from server
type Activation =
    | Activate
    | Deactivate

type Model = { 
               Counter: Counter option
               IsActive: bool 
             }
             //member __.Activation = if __.IsActive then Deactivate else Activate

// The Msg type defines what events/actions can occur while the application is running
// the state of the application changes *only* in reaction to these events
type Msg =
| Increment
| Decrement
| Activation of Activation
| InitialCountLoaded of Result<Counter, exn>

let initialCounter = fetchAs<Counter> "/api/init" (Decode.Auto.generateDecoder())

// defines the initial state and initial command (= side-effect) of the application
let init () : Model * Cmd<Msg> =
    let initialModel = { Counter = None; IsActive = false}
    let loadCountCmd =
        Cmd.ofPromise
            initialCounter
            []
            (Ok >> InitialCountLoaded)
            (Error >> InitialCountLoaded)
    initialModel, loadCountCmd



// The update function computes the next state of the application based on the current state and the incoming events/messages
// It can also run side-effects (encoded as commands) like calling the server via Http.
// these commands in turn, can dispatch messages to which the update function will react.
let update (msg : Msg) (currentModel : Model) : Model * Cmd<Msg> =
    match currentModel.Counter, msg with
    | Some counter, Increment ->
        let nextModel = { currentModel with Counter = Some { Value = counter.Value + 1 } }
        nextModel, Cmd.none
    | Some counter, Decrement ->
        let nextModel = { currentModel with Counter = Some { Value = counter.Value - 1 } }
        nextModel, Cmd.none
    | _, Activation act ->
        let nextModel = match act with
                        | Activate ->  {currentModel with IsActive = true }
                        | Deactivate -> {currentModel with IsActive = false}
        nextModel, Cmd.none
    | _, InitialCountLoaded (Ok initialCount)->
        let nextModel = { Counter = Some initialCount; IsActive = false}
        nextModel, Cmd.none

    | _ -> currentModel, Cmd.none


let safeComponents =
    let components =
        span [ ]
           [
             a [ Href "https://saturnframework.github.io" ] [ str "Saturn" ]
             str ", "
             a [ Href "http://fable.io" ] [ str "Fable" ]
             str ", "
             a [ Href "https://elmish.github.io/elmish/" ] [ str "Elmish" ]
             str ", "
             a [ Href "https://fulma.github.io/Fulma" ] [ str "Fulma" ]
           ]

    p [ ]
        [ strong [] [ str "SAFE Template" ]
          str " powered by: "
          components ]

let show = function
| { Counter = Some counter } -> string counter.Value
| { Counter = None   } -> "Loading..."

let button txt onClick =
    Button.button
        [ Button.IsFullWidth
          Button.Color IsPrimary
          Button.OnClick onClick ]
        [ str txt ]

let basicModal isActive closeDisplay =
    Modal.modal [ Modal.IsActive isActive ]
        [ Modal.background [ Props [ OnClick closeDisplay ] ] [ ]
          Modal.content [ ]
            [ Box.box' [ ]
                [ str "Test" ] ]
          Modal.close [ Modal.Close.Size IsLarge
                        Modal.Close.OnClick closeDisplay ] [ ] ]

//let toggleDisplay (modal : Fable.Import.React.ReactElement) = Modal.IsActive (not modal)



let view (model : Model) (dispatch : Msg -> unit) =
    div []
        [ 
          Navbar.navbar [ ]
              [ Navbar.Brand.div [ ]
                  [ Navbar.Item.a [ Navbar.Item.Props [ Href "#" ] ]
                      [ img [ Style [ Width "2.5em" ] // Force svg display
                              Src @"C:\Projects\fs\safe\src\Client\public\favicon.png" ] ]
                    Navbar.burger [
                        GenericOption.Props [
                            //HTMLAttr.Custom ("data-target", "myNavbar")
                            //HTMLAttr.Custom ("aria-label", "menu")
                            AriaExpanded model.IsActive
                            OnClick (fun _ -> match model.IsActive with
                                              | true -> dispatch (Activation (Deactivate))
                                              | _ -> dispatch (Activation (Activate)))
                        ]
                        CustomClass "burger"
                        CustomClass (if model.IsActive then "is-active" else "")
                    ] [
                        span [ Hidden true ] []
                        span [ Hidden true ] []
                        span [ Hidden true ] []
                    ]
                  ]
                Navbar.menu [ 
                    Navbar.Menu.Props [
                        Id "myNavbar"
                    ] 
                    Navbar.Menu.Option.CustomClass (if model.IsActive then "is-active" else "")
                ] [
                Navbar.Item.a [ Navbar.Item.HasDropdown
                                Navbar.Item.IsHoverable ]
                  [ Navbar.Link.a [ ]
                      [ str "Docs" ]
                    Navbar.Dropdown.div [ ]
                      [ Navbar.Item.a [ ]
                          [ str "Overwiew" ]
                        Navbar.Item.a [ ]
                          [ str "Elements" ]
                        Navbar.divider [ ] [ ]
                        Navbar.Item.a [ ]
                          [ str "Components" ] ] ]
                Navbar.Item.a [ Navbar.Item.HasDropdown
                                Navbar.Item.IsHoverable ]
                  [ Navbar.Link.a [ Navbar.Link.Option.IsArrowless ]
                      [ str "Link without arrow" ]
                    Navbar.Dropdown.div [ ]
                      [ Navbar.Item.a [ ]
                          [ str "Overwiew" ] ] ]
                ]
                Navbar.End.div [ ]
                  [ Navbar.Item.div [ ]
                      [ Button.button [ Button.Color IsSuccess ]
                          [ str "Demo" ] ] ] ]
          

          Container.container []
              [ Content.content [ Content.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ]
                    [ Heading.h3 [] [ str ("Press buttons to manipulate counter: " + show model) ] ]
                Columns.columns []
                    [ Column.column [] [ button "-" (fun _ -> dispatch Decrement) ]
                      Column.column [] [ button "+" (fun _ -> dispatch Increment) ] ] ]

          Footer.footer [ ]
                [ Content.content [ Content.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ]
                    [ safeComponents ] ] ]

#if DEBUG
open Elmish.Debug
open Elmish.HMR
#endif

Program.mkProgram init update view
#if DEBUG
|> Program.withConsoleTrace
|> Program.withHMR
#endif
|> Program.withReact "elmish-app"
#if DEBUG
|> Program.withDebugger
#endif
|> Program.run