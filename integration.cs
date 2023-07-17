using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web;
using System.Windows.Forms;

///----------------------------------------------------------------------------
///   Module:     DonattyIntegration
///   Author:     play_code (https://twitch.tv/play_code)
///   Email:      info@play-code.live
///   Repository: https://github.com/play-code-live/streamer.bot-donatty
///----------------------------------------------------------------------------
public class CPHInline
{
    private const string keyReference = "donatty.reference";
    private const string keyTmpToken = "donatty.tmp_token";
    private const string keyTokenAccess = "donatty.token.access";
    private const string keyTokenRefresh = "donatty.token.refresh";
    private const string keyTokenExpireAt = "donatty.token.expire_at";

    private const string DefaultHandlerAction = "DonattyHandler_Default";
    private const string DefaultHandlerAfterAction = "DonattyHandler_After";

    private PrefixedLogger Logger { get; set; }

    private Donatty.Service ClientService { get; set; }

    public void Init()
    {
        CPH.ExecuteMethod("Donatty Update Checker", "CheckAndAnnounce");

        Logger = new PrefixedLogger(CPH);
        ClientService = new Donatty.Service(new Donatty.Client(), Logger);
    }

    public void Dispose()
    {
        ClientService.UnsubscribeFromDonations();
    }

    public bool ShowAuthForm()
    {
        Form prompt = new Form()
        {
            Width = 480,
            Height = 200,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = "Введите ссылку виджета Donatty для OBS",
            StartPosition = FormStartPosition.CenterScreen,
            TopMost = true
        };

        Label widgetUrlLabel = new Label() { Left = 30, Top = 20, Width = 400, Text = "Ссылка виджета для OBS" };
        TextBox widgetUrlBox = new TextBox() { Left = 30, Top = 40, Width = 400, Text = "" };
        Button confirmation = new Button() { Text = "OK", Left = 330, Width = 100, Top = 100, DialogResult = DialogResult.OK };
        confirmation.Click += (sender, e) => { prompt.Close(); };
        prompt.Controls.Add(widgetUrlLabel);
        prompt.Controls.Add(widgetUrlBox);
        prompt.Controls.Add(confirmation);
        prompt.AcceptButton = confirmation;
        prompt.Activate();
        prompt.Focus();
        widgetUrlBox.Focus();

        bool isSubmitted = prompt.ShowDialog() == DialogResult.OK;
        if (!isSubmitted || string.IsNullOrEmpty(widgetUrlBox.Text))
            return false;

        var widgetUrl = new Uri(widgetUrlBox.Text);
        var parsedUrl = HttpUtility.ParseQueryString(widgetUrl.Query);

        var reference = parsedUrl.Get("ref");
        var token = parsedUrl.Get("token");

        if (string.IsNullOrEmpty(reference) || string.IsNullOrEmpty(token))
            return false;

        CPH.SetGlobalVar(keyTmpToken, token);
        CPH.SetGlobalVar(keyReference, reference);

        return true;
    }

    public bool ObtainToken()
    {
        var tmpToken = CPH.GetGlobalVar<string>(keyTmpToken);
        if (string.IsNullOrEmpty(tmpToken))
            return false;

        Donatty.ResponseTokenData tokenData = ClientService.ObtainToken(tmpToken);
        if (tokenData == null)
            return false;

        CPH.SetGlobalVar(keyTokenAccess, tokenData.AccessToken);
        CPH.SetGlobalVar(keyTokenRefresh, tokenData.RefreshToken);
        CPH.SetGlobalVar(keyTokenExpireAt, tokenData.ExpireAt);

        Logger.Debug("Access and refresh tokens are obtained successfully");

        return true;
    }

    public bool DonationCheckerLoop()
    {
        string accessToken = CPH.GetGlobalVar<string>(keyTokenAccess);
        string reference = CPH.GetGlobalVar<string>(keyReference);
        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(reference))
        {
            CPH.SendMessage("Необходимо выполнить авторизацию в Donatty. Введите команду !donatty_connect");
            throw new Exception("Unauthorized");
        }

        ClientService
            .OnEvent(Donatty.Service.EventDisconnected, delegate (string Event, Dictionary<string, string> Data)
            {
                Logger.Debug("Disconnected from the event stream");
            })
            .OnEvent(Donatty.Service.EventSubscribedToDonations, delegate (string Event, Dictionary<string, string> Data)
            {
                Logger.Debug("Connected to the event stream");
                CPH.SendMessage("Donatty Background Watcher is ON");
            })
            .OnEvent(Donatty.Service.EventDonation, delegate (string Event, Dictionary<string, string> Data)
            {
                Logger.Debug("Recieved a donation event: " + JsonConvert.SerializeObject(Data));

                CPH.SetArgument("donatty.donation.username", Data["username"]);
                CPH.SetArgument("donatty.donation.amount", Data["amount"]);
                CPH.SetArgument("donatty.donation.currency", Data["currency"]);
                CPH.SetArgument("donatty.donation.message", Data["message"]);

                string targetActionName = string.Format("DonattyHandler_{0}", Data["amount"]);
                if (CPH.ActionExists(targetActionName))
                    CPH.RunAction(targetActionName, false);
                else if (CPH.ActionExists(DefaultHandlerAction))
                    CPH.RunAction(DefaultHandlerAction);

                if (CPH.ActionExists(DefaultHandlerAfterAction))
                    CPH.RunAction(DefaultHandlerAfterAction);
            });


        ClientService.SubscribeToDonations(accessToken, reference);

        return true;
    }

    #region Utils
    public class PrefixedLogger
    {
        private IInlineInvokeProxy _CPH { get; set; }
        private const string Prefix = "-- Donatty:";

        public PrefixedLogger(IInlineInvokeProxy _CPH)
        {
            this._CPH = _CPH;
        }
        public void WebError(WebException e)
        {
            var response = (HttpWebResponse)e.Response;
            var statusCodeResponse = response.StatusCode;
            int statusCodeResponseAsInt = ((int)response.StatusCode);
            Error("WebException with status code " + statusCodeResponseAsInt.ToString(), statusCodeResponse);
        }
        public void Error(string message)
        {
            message = string.Format("{0} {1}", Prefix, message);
            _CPH.LogWarn(message);
        }
        public void Error(string message, params Object[] additional)
        {
            string finalMessage = message;
            foreach (var line in additional)
            {
                finalMessage += ", " + line;
            }
            Error(finalMessage);
        }
        public void Debug(string message)
        {
            message = string.Format("{0} {1}", Prefix, message);
            _CPH.LogDebug(message);
        }
        public void Debug(string message, params object[] additional)
        {
            string finalMessage = message;
            foreach (var line in additional)
            {
                finalMessage += ", " + line;
            }
            Debug(finalMessage);
        }
    }
    #endregion
}


namespace Donatty
{
    public class Service
    {
        private const string EndpointToken = "/auth/tokens/";
        private const string EventStreamUrlTemplate = "http://api-013.donatty.com/widgets/{0}/sse?zoneOffset=-180&jwt={1}";

        public const string EventDonation = "donation";
        public const string EventSubscribedToDonations = "subscribed";
        public const string EventDisconnected = "disconnected";

        private CPHInline.PrefixedLogger Logger { get; set; }
        private Client Client { get; set; }
        private EventObserver Observer { get; set; }
        private StreamReader donationStreamReader { get; set; }

        public Service(Client client, CPHInline.PrefixedLogger logger)
        {
            Client = client;
            Logger = logger;
            Observer = new EventObserver();
        }

        public ResponseTokenData ObtainToken(string tmpToken)
        {
            Logger.Debug("Fetching authorization data with temporary token");

            var result = Client.GET(EndpointToken + tmpToken);
            var tokenDataResponse = JsonConvert.DeserializeObject<Dictionary<string, ResponseTokenData>>(result);

            return tokenDataResponse["response"];
        }

        public Service OnEvent(string EventName, EventObserver.Handler handler)
        {
            Observer.Subscribe(EventName, handler);
            return this;
        }

        public void SubscribeToDonations(string token, string reference)
        {
            string url = string.Format(EventStreamUrlTemplate, reference, token);
            
            var httpClient = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

            Logger.Debug("Ready to subscribe to the donation event stream");

            using (var response = httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult())
            using (var body = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
            using (donationStreamReader = new StreamReader(body))
            {
                Observer.Dispatch(EventSubscribedToDonations);
                while (!donationStreamReader.EndOfStream)
                {
                    var line = donationStreamReader.ReadLineAsync().GetAwaiter().GetResult();
                    if (string.IsNullOrEmpty(line))
                        continue;
                    line = line.Substring(5);
                    var deserializedData = JsonConvert.DeserializeObject<ResponseDonationEvent>(line);
                    if (!string.IsNullOrEmpty(deserializedData.Error))
                    {
                        Logger.Error("Unable to fetch donation event");
                        UnsubscribeFromDonations();
                        return;
                    }
                    else if (deserializedData.Action == "PING")
                        continue;

                    var donation = deserializedData.Data;

                    Observer.Dispatch(EventDonation, new Dictionary<string, string>
                    {
                        { "username", donation.Username },
                        { "amount", donation.Amount.ToString() },
                        { "currency", donation.Currency },
                        { "message", donation.Message },
                    });
                }
            }
        }

        public void UnsubscribeFromDonations()
        {
            if (donationStreamReader == null)
                return;

            Observer.Dispatch(EventDisconnected);
            donationStreamReader.Close();
        }
    }

    public class Client
    {
        public const string TargetHost = "https://api.donatty.com";

        public string GET(string endpoint, Dictionary<string, string> parameters, Dictionary<string, string> headers)
        {
            var queryParams = new List<string>();
            foreach (var parameter in parameters)
            {
                queryParams.Add(string.Format("{0}={1}", parameter.Key, parameter.Value));
            }
            endpoint += "?" + String.Join("&", queryParams);
            return Perform(WebRequestMethods.Http.Get, TargetHost + endpoint, new Dictionary<string, string>(), headers);
        }
        public string GET(string endpoint, Dictionary<string, string> parameters)
        {
            return GET(endpoint, parameters, new Dictionary<string, string>());
        }
        public string GET(string endpoint)
        {
            return GET(endpoint, new Dictionary<string, string>());
        }
        public string POST(string endpoint, string payload, Dictionary<string, string> headers)
        {
            return Perform(WebRequestMethods.Http.Post, TargetHost + endpoint, payload, headers);
        }
        public string POST(string endpoint, Dictionary<string, string> payload, Dictionary<string, string> headers)
        {
            return Perform(WebRequestMethods.Http.Post, TargetHost + endpoint, payload, headers);
        }
        public string POST(string endpoint, Dictionary<string, string> payload)
        {
            return POST(endpoint, payload, new Dictionary<string, string>());
        }
        public string POST(string endpoint)
        {
            return POST(endpoint, new Dictionary<string, string>());
        }

        private string Perform(string method, string url, string jsonPayload, Dictionary<string, string> headers)
        {
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(url);
            webRequest.Method = method;
            webRequest.ContentType = "application/json";

            foreach (var header in headers)
            {
                webRequest.Headers.Set(header.Key, header.Value);
            }

            if (jsonPayload != string.Empty)
            {
                byte[] requestBytes = Encoding.ASCII.GetBytes(jsonPayload);
                webRequest.ContentLength = requestBytes.Length;
                Stream requestStream = webRequest.GetRequestStream();
                requestStream.Write(requestBytes, 0, requestBytes.Length);
                requestStream.Close();
            }


            var response = (HttpWebResponse)webRequest.GetResponse();
            string json = "";
            using (Stream respStr = response.GetResponseStream())
            {
                using (StreamReader rdr = new StreamReader(respStr, Encoding.UTF8))
                {
                    json = rdr.ReadToEnd();
                    rdr.Close();
                }
            }

            return json;
        }
        private string Perform(string method, string url, Dictionary<string, string> payload, Dictionary<string, string> headers)
        {
            string payloadString = "";
            if (payload.Count > 0)
                payloadString = JsonConvert.SerializeObject(payload);

            return Perform(method, url, payloadString, headers);
        }
    }

    public class EventObserver
    {
        public delegate void Handler(string Event, Dictionary<string, string> Data = null);
        private Dictionary<string, List<Handler>> Handlers { get; set; }

        public EventObserver()
        {
            Handlers = new Dictionary<string, List<Handler>>();
        }
        public EventObserver Subscribe(string EventName, Handler handler)
        {
            if (!Handlers.ContainsKey(EventName))
                Handlers.Add(EventName, new List<Handler>());

            Handlers[EventName].Add(handler);
            return this;
        }
        public void Dispatch(string EventName, Dictionary<string, string> Data = null)
        {
            if (!Handlers.ContainsKey(EventName) || Handlers[EventName].Count == 0)
                return;

            foreach (var handler in Handlers[EventName])
            {
                handler(EventName, Data);
            }
        }
    }

    public class ResponseTokenData
    {
        [JsonProperty("accessToken")]
        public string AccessToken;
        [JsonProperty("refreshToken")]
        public string RefreshToken;
        [JsonProperty("expireAt")]
        public string ExpireAt;
    }

    public class ResponseDonationEvent
    {
        [JsonProperty("error")]
        public string Error;
        [JsonProperty("action")]
        public string Action;
        [JsonProperty("data")]
        public ResponseDonationEventData Data = null;
    }

    public class ResponseDonationEventData
    {
        [JsonProperty("subscriber")]
        public string Username;
        [JsonProperty("message")]
        public string Message;
        [JsonProperty("amount")]
        public double Amount;
        [JsonProperty("currency")]
        public string Currency;
    }
}
