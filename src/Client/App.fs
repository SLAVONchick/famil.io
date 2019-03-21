module Client

open Elmish
open Elmish.React

open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.PowerPack.Fetch

open Thoth.Json

open Shared
open Home


open Fulma
open Fable.Helpers
open Fable.Import
open System.Drawing
open Fulma
open Fulma



type Page =
    | Home
    | Description

type Stage = CurrentPage of Page

type Url = Url of string

type Activation =
    | Activate
    | Deactivate


type Msg =
    | Activation of Activation
    | NavigateTo of Url

type Model = {
               Stage : Stage
               IsActive : bool
             }
             member __.Activation = if __.IsActive then Deactivate else Activate

             member this.SubView =
                match this.Stage with
                | CurrentPage (Home) -> Home.view
                | CurrentPage (Description) -> Description.view


let init () : Model * Cmd<Msg> =
    let initialModel = { IsActive = false; Stage = CurrentPage(Home) }
    let initialCmd =
        Cmd.ofMsg(NavigateTo(Url "/home"))
    initialModel, initialCmd




let getNextActive act =
    match act with
    | Activate ->  true
    | Deactivate -> false

let update (msg : Msg) (currentModel : Model) : Model * Cmd<Msg> =
    match msg with
    | Activation act ->
        let nextModel = {currentModel with IsActive = getNextActive act; }
        nextModel, Cmd.none

    | NavigateTo (Url "/home") ->
        let nextModel = {currentModel with Stage = CurrentPage Home}
        nextModel, Cmd.none

    | NavigateTo (Url "/description") ->
        let nextModel = {currentModel with Stage = CurrentPage Description}
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


let button txt onClick =
    Button.button
        [ Button.IsFullWidth
          Button.Color IsPrimary
          Button.OnClick onClick ]
        [ str txt ]




let view (model : Model) (dispatch : Msg -> unit) =

    let currentUrl =
        match model.Stage with
        | CurrentPage (Page.Home _ ) -> "/home "
        | CurrentPage (Page.Description _) -> "/description"

    let navItem nextUrl title =
        Navbar.Item.a [ Navbar.Item.IsHoverable ] [
            Button.button [
                Button.Option.OnClick (fun _ -> dispatch (NavigateTo (Url nextUrl)))
                Button.IsHovered true
                Button.Color IsInfo ]
              [ str title ] ]
    div []
        [
          Navbar.navbar [ ]
              [ Navbar.Brand.div [ ]
                  [ Navbar.Item.a [ ]
                      [ img [ Style [ Width "2.5em" ]
                              Src @"/public/favicon.png" ] ]
                    Navbar.burger [
                        GenericOption.Props [
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
                    Navbar.Menu.Modifiers [
                        Modifier.BackgroundColor Color.IsPrimary
                        Modifier.TextColor Color.IsBlack ]
                    Navbar.Menu.Option.CustomClass (if model.IsActive then "is-active" else "")
                ] [
                navItem "/home" "Home"
                navItem "/description" "Description"
                ]
                Navbar.End.div [ ]
                  [ Navbar.Item.div [ ]
                      [ Button.button [ Button.Color IsSuccess ]
                          [ str "Demo" ] ] ] ]


          Container.container []
              [ model.SubView ]

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