module Home

open Fulma
open Fable.Helpers.React
open Fable.Helpers.React.Props

let view() = div [] [ 
        Heading.h1 [
            Heading.Modifiers [
                Modifier.Display (Screen.All, Display.Block)
            ]
        ] [ 
                str "Welcome to tasks-for-my.family!"
            ] 
        Heading.h6 [] [
            str "\"Tasks for my family\" is a fast and simple task tracker for family members (in main purpose)."
        ]
        Heading.h6 [] [
            str "All you need to begin using \"tasks for my family\" is create group, which is used to group tasks. Or, if there is already a group, created by your mates, just ask them to add you to it."
        ]
    ]