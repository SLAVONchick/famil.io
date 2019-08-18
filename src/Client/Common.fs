module Client.Common

open Fulma
open Fable.Helpers.React

let button txt onClick =
  Button.button [ Button.IsFullWidth
                  Button.Color IsPrimary
                  Button.OnClick onClick ] [ str txt ]

let navItem nextUrl title dispatch =
  Button.button [ Button.Option.OnClick(fun _ -> dispatch nextUrl)
                  Button.IsHovered true
                  Button.Color IsInfo ] [ str title ]

let modal header content footer dispatch =
  Modal.modal [ Modal.Option.CustomClass "is-active" ]
    [ Modal.background [] []
      Modal.Card.card [] [ Modal.Card.head []
                             [ Modal.Card.title [] [ p [] [ str header ] ]
                               Button.button [ Button.Option.CustomClass "delete"
                                               Button.OnClick(fun _ -> dispatch()) ] [] ]

                           Modal.Card.body [ CustomClass "is-active" ]
                             [ Content.content [] content ]
                           Modal.Card.foot [] footer ] ]

open Elmish.Browser.UrlParser

let i64 state =
  custom "i64" (System.Int64.TryParse
                >> function
                | true, value -> Ok value
                | _ -> Error "Can't parse int") state
