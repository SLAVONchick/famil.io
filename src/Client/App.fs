module Client.App

open Elmish.Browser.UrlParser
open Elmish
open Elmish.React
open Fulma
open Fable.Import
open Elmish.Browser.Navigation
open Client.Common
open System.Text.RegularExpressions

let regex = Regex(@"(?<name>\#group/)(?<value>\d{1,19})")

let isGroup = regex.IsMatch

let getGroupId input =
    let matches = regex.Match(input)
    System.Int64.Parse matches.Groups.[2].Value
type Page =
    | DefaultPage
    | Home
    | Description
    | Account
    | Groups
    | Group of int64

    override x.ToString() =
        match x with
        | DefaultPage -> "#"
        | Home -> "#home"
        | Description -> "#description"
        | Account -> "#account"
        | Groups -> "#groups"
        | Group id -> sprintf "#group/%d" id

//type Stage = CurrentPage of Page

type Url = Url of string

type Activation =
    | Activate
    | Deactivate


type Msg =
    | Activation of Activation
    | NavigateTo of Url
    | AccountMsg of Account.Msg
    | GroupsMsg of Groups.Msg
    | GroupMsg of Client.Group.Msg

type Model =
    { Stage : Page
      IsActive : bool
      Account: Account.State
      Groups: Groups.State
      Group: Client.Group.State }

    member __.Activation = if __.IsActive then Deactivate else Activate

    //member this.SubView getEl =
    //      match this.Stage with
    //      | DefaultPage
    //      | Home -> Home.view()
    //      | Description -> Description.view()
    //      | Account -> getEl()


let route =
    oneOf [
        map DefaultPage (s "" </> top)
        map Home (s "home" </> top)
        map Description (s "description" </> top)
        map Account (s "account" </> top)
        map Groups (s "groups" </> top)
        map Group (s "group/" </> i64)
    ]


open System
open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.PowerPack
open Thoth.Json


let urlUpdate (result: Page option) model =
    match result with
    | Some _ ->
        {model with Stage = result.Value},  Navigation.modifyUrl (result.Value.ToString())
    | None ->
        {model with Stage = Home},  Navigation.modifyUrl (Page.Home.ToString())


let init (page: Page option): Model * Cmd<Msg> =
    let initialAccountState, initialAccountCmd = Account.init()
    let initialGroupsState, initialGroupsCmd = Groups.init()
    let initialGroupState, initialGroupCmd = Client.Group.init()
    let initialModel =
        { IsActive = false
          Stage = Home
          Account = initialAccountState
          Groups = initialGroupsState
          Group = initialGroupState }
    let initialCmd =
        Cmd.batch [
          Cmd.map NavigateTo (Cmd.ofMsg(Url ((page |> Option.defaultValue Home).ToString())))
          Cmd.map AccountMsg initialAccountCmd
          Cmd.map GroupsMsg initialGroupsCmd
          Cmd.map GroupMsg initialGroupCmd
        ]
    initialModel, initialCmd




let getNextActive act =
    match act with
    | Activate ->  true
    | Deactivate -> false

let logInOrOut inOrOut model =
    let mutable url = ""
#if DEBUG
    url <- "http://localhost:8085"
        +  "/api/"
        + (if inOrOut then "login/" else "logout/")
        + Browser.window.location.href
#else
    url <- (Browser.window.location.protocol + "//" + Browser.window.location.host)
        +  "/api/"
        + (if inOrOut then "login/" else "logout/")
        + Browser.window.location.href
#endif
    Browser.window.location.assign url
    model, Cmd.none

let update (msg : Msg) (currentModel : Model) : Model * Cmd<Msg> =
    match msg with
    | Activation act ->
        let nextModel = {currentModel with IsActive = getNextActive act; }
        nextModel, Cmd.none
    | NavigateTo u ->
        match u with
        | (Url "#description") ->
            let nextModel = {currentModel with Stage = Description}
            nextModel, Navigation.modifyUrl "#description"
        | (Url "#login") ->
            logInOrOut true currentModel
        | (Url "#logout") ->
            logInOrOut false currentModel
        | (Url "#account") ->
            let nextModel = {currentModel with Stage = Account}
            nextModel, Navigation.modifyUrl "#account"
        | (Url "#groups") ->
            let nextModel = {currentModel with Stage = Groups}
            nextModel, Navigation.modifyUrl "#groups"
        | Url v  when v |> isGroup ->
            let id = getGroupId v
            let nextModel = {currentModel with Stage = Group id}
            nextModel, Navigation.modifyUrl (nextModel.Stage.ToString())
        | Url v ->
            printfn "%s %b" v (isGroup v)
            let nextModel = {currentModel with Stage = Home}
            nextModel, Navigation.modifyUrl "#home"
    | AccountMsg a ->
        let nextAccountState, nextAccountCmd = Account.update currentModel.Account a
        let nextModel = { currentModel with Account = nextAccountState}
        nextModel, Cmd.map AccountMsg nextAccountCmd
    | GroupsMsg gs ->
        let nextGroupsState, nextGroupsCmd = Groups.update currentModel.Groups gs
        let nextModel = {currentModel with Groups = nextGroupsState}
        nextModel, Cmd.map GroupsMsg nextGroupsCmd
    | GroupMsg g ->
        let nextGroupState, nextGroupCmd = Client.Group.update currentModel.Group g
        let nextModel = {currentModel with Group = nextGroupState}
        nextModel, Cmd.map GroupMsg nextGroupCmd


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





let view (model : Model) (dispatch : Msg -> unit) =
    let navDispatch = Url >> NavigateTo >> dispatch
    div []
        [
          yield (
           match model.Account with
           | Account.Initial ->
               dispatch (Account.StartLoading |> AccountMsg)
               div [] []
           | Account.Loading ->
               div [] []
           | Account.Loaded _ ->
               div [] [])
          yield Navbar.navbar [ ]
              [ Navbar.Brand.div [ ]
                  [ Navbar.Item.a [ ]
                      [ img [ Style [ Width "2em" ]
                              Src @"./favicon.png" ] ]
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
                Navbar.Item.a [] [ navItem "#home" "Home" navDispatch ]
                Navbar.Item.a [] [ navItem "#description" "Description" navDispatch ]
                (if Account.isAuthorized model.Account then
                    Navbar.Item.a [] [ navItem "#account" "Account" navDispatch ]
                 else div [] [] )
                (if Account.isAuthorized model.Account then
                    Navbar.Item.a [] [navItem "#groups" "Groups" navDispatch]
                 else div [] [] )
                ]
                Navbar.End.div [ ]
                  [ Navbar.Item.div [ ]
                      [ Account.loginButton navItem navDispatch model.Account ]
                  ]
              ]
          match model.Stage with
          | DefaultPage
          | Home ->
              yield Home.view ()
          | Description ->
              yield Description.view ()
          | Account ->
              yield Account.view model.Account (AccountMsg >> dispatch)
          | Groups ->
              yield Groups.groupsView
                  (Account.getUserId model.Account)
                  model.Groups
                  (GroupsMsg >> dispatch)
                  (Url >> NavigateTo >> dispatch)
          | Group id ->
              yield Client.Group.groupsView
                  (Group.getUserList model.Group)
                  (Account.getUserId model.Account)
                  id
                  model.Group
                  (GroupMsg >> dispatch)


          yield Footer.footer [ ]
                [ Content.content [ Content.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ]
                    [ safeComponents ] ] ]

#if DEBUG
open Elmish.Debug
#endif
open Elmish.HMR

Program.mkProgram init update view
|> Program.toNavigable (parseHash route) urlUpdate
#if DEBUG
|> Program.withConsoleTrace
|> Program.withHMR
#endif
|> Program.withReact "elmish-app"
#if DEBUG
|> Program.withDebugger
#endif
|> Program.run
