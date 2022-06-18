open System


open RestSharp
open FSharp.Data

//  dotnet publish MeekoDump.fsproj -r win-x64 -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true

let client = new RestClient("https://api.meeko.app/family/v1");

type loginType = { email: string; password: string;}
type registerType = { token: string; device_os: string; device_os_version: string; device_app_version: string;}
let LoginAndRegister email password =
  
    let request = new RestRequest("/login", Method.POST, DataFormat.Json );
    let loginObj = { email =  email; password = password; }

    request.AddJsonBody(loginObj)  |> ignore
    let response = client.Execute(request);
    let t = JsonValue.Parse(response.Content);
    let token = t.TryGetProperty("token").Value.ToString().Replace("\"","")
   
    let registerObj = { token = token; device_os = "android"; device_os_version = "11" ; device_app_version = "1.2"}

    let registerrequest = new RestRequest("/gcm/register", Method.POST);
    registerrequest.AddParameter("Authorization", "Bearer " + token, ParameterType.HttpHeader)  |> ignore
    registerrequest.AddJsonBody(registerObj)  |> ignore
    let response = client.Execute(registerrequest);
    token

let getPhotosJson pageNumber token =
    let request2 = new RestRequest("photos",  Method.GET)
    request2.AddParameter("Authorization", "Bearer " + token, ParameterType.HttpHeader)  |> ignore
    request2.AddParameter( "per_page", 6) |> ignore
    if (pageNumber > 0) then
        request2.AddParameter("page",pageNumber)|> ignore
    let response2 = client.Get(request2);
    response2.Content

let webclient = new System.Net.WebClient();

let SavePhotos (data : JsonValue) savePath =
    for record in data do
         let url = record.GetProperty("photo_url").AsString()
         let epoch = record.GetProperty("taken_at").AsInteger64()
         let id = record.GetProperty("id").AsString()
         let dt =  System.DateTimeOffset.FromUnixTimeSeconds(epoch)
         let filename = dt.DateTime.ToString("yyyyMMdd_hhmmss") + " " + id + ".jpg"
         printfn "%s" filename
         let file = System.IO.Path.Combine(savePath, filename)
         webclient.DownloadFile(url, file)


let RegisterDevProxy (host : string) (port:int)= 
    let certificatevalidationfunction a b c d = true
    client.Proxy <- new System.Net.WebProxy(host,port);
    client.RemoteCertificateValidationCallback <- new System.Net.Security.RemoteCertificateValidationCallback( certificatevalidationfunction);
    printfn "proxy registered"
    

[<EntryPoint>]
let main argv =
    if argv.Length<2 then failwith "MeekoDump login password"

    let login = argv.[0]
    let password = argv.[1]

    if argv.Length=3 && argv.[2]="dev" then RegisterDevProxy "localhost" 8000
    
    let token = LoginAndRegister login password;

    let photos = getPhotosJson 0 token
    let parsedPhotos = JsonValue.Parse(photos)
    let lastpage = parsedPhotos.GetProperty("last_page").AsInteger()



    let savePath = System.IO.Path.Combine(System.IO.DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)).ToString(), "Meeko");
    System.IO.Directory.CreateDirectory(savePath) |> ignore

    for i in 0 .. lastpage do
        let photos = getPhotosJson i token
        let parsedPhotos = JsonValue.Parse(photos)
        let data = parsedPhotos.GetProperty("data")
        SavePhotos data savePath     

    printfn "%s%s"  "Successfully exported pictures to " savePath
    
    0 // return an integer exit code
