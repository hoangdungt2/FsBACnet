namespace FsBACnet
open System.IO.BACnet
open System.Text.RegularExpressions

type ReadResult<'a> = 'a option
type BACnetPoint =
    {
        PointString  : string; // "105123 AV123"for AV point 123 at device 105123
        BacnetObj    : BacnetObjectId; // be parsed from string        
        DeviceAdr    : BacnetAddress;  // be parsed from string
        Value        : ReadResult<float>; // read from obj and adr
    }
///
/// - BACnetPoint module
/// 
module BACnetPoint = 
    let private longName2Short = [  ("OBJECT_ANALOG_VALUE", "AV")
                                    ("OBJECT_ANALOG_INPUT", "AI")
                                    ("OBJECT_ANALOG_OUTPUT","AO")
                                    ("OBJECT_BINARY_VALUE", "BV")
                                    ("OBJECT_BINARY_INPUT", "BI")
                                    ("OBJECT_BINARY_OUTPUT","BO")
                                 ] |> Map    
    let private getObjType (objType:string) = 
        match objType.ToLower() with
        | "av" -> BacnetObjectTypes.OBJECT_ANALOG_VALUE
        | "ai" -> BacnetObjectTypes.OBJECT_ANALOG_INPUT
        | "ao" -> BacnetObjectTypes.OBJECT_ANALOG_OUTPUT
        | "bv" -> BacnetObjectTypes.OBJECT_BINARY_VALUE
        | "bi" -> BacnetObjectTypes.OBJECT_BINARY_INPUT
        | "bo" -> BacnetObjectTypes.OBJECT_BINARY_OUTPUT
        | _ -> sprintf "[getObjType]: cannot parse objType from `%s`" objType
               |> failwith; BacnetObjectTypes.OBJECT_ANALOG_VALUE
    let private getBacnetObjectId (objStr:string) = 
    // objStr can be "AV-1" or "BI0" ...
        let objType = Regex(@"^[a-z]{2}", RegexOptions.IgnoreCase).Match(objStr).Value |> getObjType
        BacnetObjectId(objType, Regex(@"[0-9]{1,4}").Match(objStr).Value |> uint32)
    let private helperObjTypeToString (bObj:BacnetObjectId) = 
        Map.find (bObj.``type``.ToString()) longName2Short
    let private helperGetDevFromString (pointStr:string) = 
        let devMatch = Regex(@"(^dev {0,1}[0-9]{1,8}|^[0-9]{1,8})", RegexOptions.IgnoreCase).Match(pointStr)
        if devMatch.Success then devMatch.Value.ToLower().Replace("dev","") |> uint32 else failwith (sprintf "Cannot Find deviceId in string"); 0ul

    let private helperFormalizePointString (dev:uint32) (bobj:BacnetObjectId) = 
        sprintf "%d %s-%d" dev (helperObjTypeToString bobj) (bobj.instance)

    let normalizePointString (pointStr:string) = 
        let point = Regex(@"([a-z]{2}\-|[a-z]{2})[0-9]{1,4}", RegexOptions.IgnoreCase).Match(pointStr).Value |> getBacnetObjectId
        let device = helperGetDevFromString pointStr
        helperFormalizePointString device point
    let parsePointString (pointStr:string) = 
        let point = Regex(@"([a-z]{2}\-|[a-z]{2})[0-9]{1,4}", RegexOptions.IgnoreCase).Match(pointStr).Value |> getBacnetObjectId
        let device = helperGetDevFromString pointStr
        let bacAddr = 
            match device.ToString() |> BACnetDeviceStore.getDevice with
            | Some x -> x
            | _ -> failwith (sprintf "Device `%d` is not online" device)
        {
            PointString = helperFormalizePointString device point
            BacnetObj   = point
            DeviceAdr   = bacAddr
            Value       = None 
        }
    let private formBACnetPointVal (x:BACnetPoint) (value:float) = 
        {x with Value = value |> Some}
    let readValueAsync (client:BacnetClient) (point:BACnetPoint) =         
        async{
            try
                let! res = 
                    client.ReadPropertyAsync(point.DeviceAdr,point.BacnetObj.Type,point.BacnetObj.instance, BacnetPropertyIds.PROP_PRESENT_VALUE ) 
                    |> Async.AwaitTask 
                return res.[0].Value.ToString() 
                        |> System.Convert.ToDouble 
                        |> formBACnetPointVal point
            with _ -> return {point with Value = None}
        }
    
    let readValue (client:BacnetClient) (point:BACnetPoint) = 
        readValueAsync client point 
        |> Async.RunSynchronously



    let writePointAsync (client:BacnetClient) (point:BACnetPoint) (value:float32) = 
        async{
            let bacnetValue = 
                match point.BacnetObj.``type`` with 
                | BacnetObjectTypes.OBJECT_ANALOG_OUTPUT| BacnetObjectTypes.OBJECT_ANALOG_VALUE ->
                    BacnetValue( BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, value )
                | _ -> BacnetValue( BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, System.Convert.ToInt32 value )
            return client.WritePropertyRequest( point.DeviceAdr, point.BacnetObj, BacnetPropertyIds.PROP_PRESENT_VALUE, seq{yield bacnetValue} )
        }
    let writeDisplayResult (client:BacnetClient) (point:BACnetPoint) (value:float32) = 
        async{
            let! writeRes = writePointAsync client point value
            printfn "Write %.2f to `%s` ... %b" value point.PointString writeRes 
            return ()
        }
