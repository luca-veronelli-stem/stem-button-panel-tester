module Stem.ButtonPanel.Tester.Tests.Core.Dictionary.InstallationTests

open System
open Xunit
open FsCheck
open FsCheck.Xunit
open Stem.ButtonPanel.Tester.Core.Dictionary

[<Fact>]
let ``Two installations with the same MachineName, UserSid, InstallationId are structurally equal`` () =
    let id = Guid.NewGuid()
    let a = { MachineName = "WS-01"; UserSid = "S-1-5-21-1"; InstallationId = id }
    let b = { MachineName = "WS-01"; UserSid = "S-1-5-21-1"; InstallationId = id }
    Assert.Equal(a, b)

[<Fact>]
let ``Differing InstallationId makes records unequal under full F# equality`` () =
    let a = { MachineName = "WS-01"; UserSid = "S-1-5-21-1"; InstallationId = Guid.NewGuid() }
    let b = { a with InstallationId = Guid.NewGuid() }
    Assert.NotEqual<Installation>(a, b)

[<Fact>]
let ``installationsMatch returns true when only InstallationId differs`` () =
    let a = { MachineName = "WS-01"; UserSid = "S-1-5-21-1"; InstallationId = Guid.NewGuid() }
    let b = { a with InstallationId = Guid.NewGuid() }
    Assert.True(Installation.installationsMatch a b)

[<Fact>]
let ``installationsMatch returns false when MachineName differs`` () =
    let a = { MachineName = "WS-01"; UserSid = "S-1-5-21-1"; InstallationId = Guid.NewGuid() }
    let b = { a with MachineName = "WS-02" }
    Assert.False(Installation.installationsMatch a b)

[<Fact>]
let ``installationsMatch returns false when UserSid differs`` () =
    let a = { MachineName = "WS-01"; UserSid = "S-1-5-21-1"; InstallationId = Guid.NewGuid() }
    let b = { a with UserSid = "S-1-5-21-2" }
    Assert.False(Installation.installationsMatch a b)

[<Property>]
let ``installationsMatch is reflexive`` (machine: NonEmptyString) (sid: NonEmptyString) (id: Guid) =
    let inst = { MachineName = machine.Get; UserSid = sid.Get; InstallationId = id }
    Installation.installationsMatch inst inst

[<Property>]
let ``installationsMatch is symmetric`` (machine: NonEmptyString) (sid: NonEmptyString) (id1: Guid) (id2: Guid) =
    let a = { MachineName = machine.Get; UserSid = sid.Get; InstallationId = id1 }
    let b = { a with InstallationId = id2 }
    Installation.installationsMatch a b = Installation.installationsMatch b a
