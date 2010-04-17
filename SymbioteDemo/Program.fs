module Progam

open System
open Symbiote.Core
open Symbiote.Relax
open Symbiote.Daemon
open Symbiote.Jackalope
open Symbiote.Jackalope.Impl

type PersonCouchDocument =
    val id : string
    val mutable name : string
    val mutable address : string
    inherit DefaultCouchDocument 
        new (id, name, address) = 
            {id = id; name = name; address = address} then base.DocumentId <- id
        member x.Name
            with get () = x.name
        member x.Address
            with get () = x.address

type HandleMessage() =
    interface IMessageHandler<PersonCouchDocument> with
        member x.Process (message:PersonCouchDocument, messageDelivery:IMessageDelivery) =
            Console.WriteLine("Processing message for person {0}.", message.name)
            messageDelivery.Acknowledge()

type DemoService = 
    val couchServer : ICouchServer
    val bus : IBus
    new(couchServer, bus) as this = 
        {couchServer = couchServer; bus = bus} then this.Initialize()
    member public x.Initialize () =
        x.bus.AddEndPoint(fun x -> 
            x.Exchange("SymbioteDemo", ExchangeType.fanout).QueueName("SymbioteDemo")
                .Durable().PersistentDelivery() |> ignore)
    interface IDaemon with
        member x.Start () =
            do Console.WriteLine("The service has started")
            x.bus.Subscribe("SymbioteDemo", null)
            let document = new PersonCouchDocument("123456", "John Doe", "123 Main")
            x.couchServer.Repository.Save(document)
            x.bus.Send("SymbioteDemo", document)
            let documentRetrieved = 
                x.couchServer.Repository.Get<PersonCouchDocument>(document.DocumentId);
            do Console.WriteLine(
                "The document with name {0} and address {1} was retrieved successfully", 
                documentRetrieved.Name, documentRetrieved.Address)
            do x.couchServer.DeleteDatabase<PersonCouchDocument>()
        member x.Stop () =
            do Console.WriteLine("The service has stopped")

do Assimilate
    .Core()
    .Daemon(fun x -> (x.Name("FSharpDemoService")
                                    .DisplayName("An FSharp Demo Service")
                                    .Description("An FSharp Demo Service")
                                    .Arguments([||]) |> ignore))
    .Relax(fun x -> x.Server("localhost") |> ignore) 
    .Jackalope(fun x -> x.AddServer(fun s -> s.Address("localhost").AMQP08() |> ignore) |> ignore)
    .RunDaemon() 
