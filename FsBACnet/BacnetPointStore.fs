namespace FsBACnet
open System.IO.BACnet


module BACnetPointStore = 
    /// - MAILBOX 2
    type private MsgBacnetPoint = 
    | Put of BACnetPoint
    | UpdateValue of BacnetClient
    | GetAll of AsyncReplyChannel<List<BACnetPoint>>
    | IsExist of string*AsyncReplyChannel<bool>
    | TryFind of string*AsyncReplyChannel<BACnetPoint option>
    | TryFindByName of string*AsyncReplyChannel<BACnetPoint option>

    let private helperCheckPointExist (pointList:BACnetPoint list) (pointString:string) = 
        List.exists (fun x-> x.PointString.ToLower()=pointString.ToLower()) pointList
    type private BacnetPointMailbox() = 
        static let mailBoxFunc (inbox:MailboxProcessor<MsgBacnetPoint>) = 
            let rec handlerMsg (state:List<BACnetPoint>) = 
                async{
                    let! msg = inbox.Receive()
                    let newState = match msg with
                                   | Put p -> if helperCheckPointExist state p.PointString then state else List.append state [p]
                                   | GetAll reply -> reply.Reply(state); state
                                   | IsExist (pointString,reply) -> helperCheckPointExist state pointString |> reply.Reply ; state
                                   | TryFind (pointString,reply) -> List.tryFind (fun x-> x.PointString.ToLower()=pointString.ToLower()) state |> reply.Reply ; state
                                   | TryFindByName (pointName,reply) -> List.tryFind (fun x-> x.Name.ToLower()=pointName.ToLower()) state |> reply.Reply ; state
                                   | UpdateValue client -> state |> List.map (BACnetPoint.readValueAsync client) |> Async.Parallel |> Async.RunSynchronously |> Array.toList
                    return! handlerMsg newState                                              
                }
            handlerMsg List.empty
        static let agent = MailboxProcessor<MsgBacnetPoint>.Start(mailBoxFunc)                    
        static member Put (fn:BACnetPoint) = 
            agent.Post (Put fn)
        static member GetAll () = 
            agent.PostAndReply(GetAll)
        static member IsExist (name:string) = 
            agent.PostAndReply (fun reply -> IsExist(name,reply))      
        static member TryFind (pointString:string) = 
            agent.PostAndReply (fun reply -> TryFind(pointString,reply))      
        static member TryFindByName (pointName:string) = 
            agent.PostAndReply (fun reply -> TryFind(pointName,reply))      
        static member UpdateValue (client:BacnetClient) = 
            agent.Post (UpdateValue client)            
    let getPoint (pointString:string) = 
        pointString
        |> BACnetPoint.normalizePointString
        |> BacnetPointMailbox.TryFind
    let getPointByName (pointName:string) = 
        BacnetPointMailbox.TryFindByName pointName
    let getPoints = 
        BacnetPointMailbox.GetAll
    let putPoint (point:BACnetPoint) = 
        BacnetPointMailbox.Put point
    let putPoints (points:BACnetPoint list) = 
        List.iter putPoint points
    let updateValues (client:BacnetClient) = 
        BacnetPointMailbox.UpdateValue client
