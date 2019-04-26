module Client

open Elmish.Browser.UrlParser

open Elmish
open Elmish.React

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
open Elmish.Browser.Navigation
open Fable.Import
open System
open Fable.Core
open Fable.Import.React
open Fable.Helpers.React.ReactiveComponents



type Page =
    | DefaultPage
    | Home
    | Description
    | Login
    | Account

    override x.ToString() =
        match x with
        | DefaultPage -> "/"
        | Home -> "/home"
        | Description -> "/description"
        | Login -> "/login"
        | Account -> "/account"

//type Stage = CurrentPage of Page

type Url = Url of string

type Activation =
    | Activate
    | Deactivate


type Msg =
    | Activation of Activation
    | NavigateTo of Url
    | AccountMsg of Account.Msg

type Model =
    { Stage : Page
      IsActive : bool
      Account: Account.State }

    member __.Activation = if __.IsActive then Deactivate else Activate

    member this.SubView getEl =
          match this.Stage with
          | DefaultPage
          | Login
          | Home -> Home.view()
          | Description -> Description.view()
          | Account -> getEl()


let route =
    oneOf [
        map DefaultPage (s "/" </> top)
        map Home (s "/home" </> top)
        map Description (s "/description" </> top)
        map Login (s "/login" </> top)
        map Account (s "/account" </> top)
    ]


open Fable.Helpers.React
open Fable.Helpers.React.Props


let urlUpdate (result: Page option) model =
    match result with
    | Some Description ->
        {model with Stage = result.Value}, Navigation.modifyUrl "/account"
    | Some Account ->
        {model with Stage = result.Value}, Navigation.modifyUrl "/account"
    | Some DefaultPage
    | Some (Home) ->
        {model with Stage = result.Value},  Navigation.modifyUrl "/home"
    | _ -> model,  Navigation.modifyUrl "/home"


let init page: Model * Cmd<Msg> =
    let inittialAccountState, initialAccountCmd = Account.init()
    let initialModel = { IsActive = false; Stage = Home; Account = inittialAccountState }
    let initialCmd =
        Cmd.batch [
          Cmd.map NavigateTo (Cmd.ofMsg(Url "/home"))
          Cmd.map AccountMsg initialAccountCmd
        ]
    initialModel, initialCmd




let getNextActive act =
    match act with
    | Activate ->  true
    | Deactivate -> false
let login model = 
    let url = ("http://localhost:8085/api/login/" + "http://localhost:8080" + model.Stage.ToString())
    Browser.window.location.assign url
    model, Cmd.none

let update (msg : Msg) (currentModel : Model) : Model * Cmd<Msg> =
    match msg with
    | Activation act ->
        let nextModel = {currentModel with IsActive = getNextActive act; }
        nextModel, Cmd.none
    | NavigateTo u -> 
        match u with 
        | (Url "/description") ->
            let nextModel = {currentModel with Stage = Description}
            nextModel, Navigation.modifyUrl "/description"
        | (Url "/account") ->
            match currentModel.Account with
            | Account.State.Initial -> login currentModel
            | Account.State.Loading -> currentModel, Cmd.none
            | Account.State.Loaded us -> 
                match us with 
                | Account.UserStatus.NotAuthorized _ -> login currentModel
                | Account.UserStatus.Authorized _ -> 
                    let nextModel = {currentModel with Stage = Account}
                    nextModel, Navigation.modifyUrl "/account"
        | _ ->
            let nextModel = {currentModel with Stage = Home}
            nextModel, Navigation.modifyUrl "/home"
    | AccountMsg a ->
        let nextAccountState, nextAccountCmd = Account.update a currentModel.Account
        let nextModel = { currentModel with Account = nextAccountState}
        nextModel, Cmd.map AccountMsg nextAccountCmd


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

let navItem nextUrl title dispatch =
      Button.button [
                Button.Option.OnClick (fun _ ->
                    dispatch (NavigateTo (Url nextUrl)))
                Button.IsHovered true
                Button.Color IsInfo ]
              [ str title ]
              
let logInOrOut dispatch =
    match Account.isAuthorized with
    | true ->
        Navbar.Item.a [ Navbar.Item.Option.HasDropdown ] [
            navItem "/account" "More" dispatch
            navItem "/logout" "Logout" dispatch
        ]
    | false -> 
        //if not loadingStarted then dispatch (AccountMsg Account.Msg.StartLoading) else loadingStarted <- true
        Navbar.Item.a [ ] [
            navItem "/login" "Login" dispatch 
        ]


let view (model : Model) (dispatch : Msg -> unit) =
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
                navItem "/home" "Home" dispatch
                navItem "/description" "Description" dispatch
                ]
                Navbar.End.div [ ]
                  [ Navbar.Item.div [ ]
                      [ logInOrOut dispatch ]
                  ]
              ]
          model.SubView (fun () -> div [ ] [ ] )
          Footer.footer [ ]
                [ Content.content [ Content.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ]
                    [ safeComponents ] ] ]

#if DEBUG
open Elmish.Debug
open Elmish.HMR
#endif

Program.mkProgram init update view
|> Program.toNavigable (parsePath route) urlUpdate
#if DEBUG
|> Program.withConsoleTrace
|> Program.withHMR
#endif
|> Program.withReact "elmish-app"
#if DEBUG
|> Program.withDebugger
#endif
|> Program.run