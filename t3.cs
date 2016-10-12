    public class t3
    {
        public void t3()
        {
            dynamic context = null;
            context.Request.Form["IntProperty_00"] = "1";
            context.Request.Form["IntProperty_01"] = "2";
            context.Request.Form["IntProperty_02"] = "3";
            context.Request.Form["IntProperty_03"] = "4";
            context.Request.Form["IntProperty_04"] = "5";
            context.Request.Form["IntProperty_05"] = "6";
            context.Request.Form["IntProperty_06"] = "7";
            context.Request.Form["IntProperty_07"] = "8";
            context.Request.Form["IntProperty_08"] = "9";
            context.Request.Form["IntProperty_09"] = "10";
            context.Request.Form["IntProperty_10"] = "11";
            context.Request.Form["IntProperty_11"] = "12";
        }
		
		void processResponseOk(GcmAsyncParameters asyncParam)
		{
			var result =new GcmMessageTransportResponse()
			{
				ResponseCode = GcmMessageTransportResponseCode.Ok,
				Message = asyncParam.Message
			};
			
			var json = new JObject();
		    var str = string.Empty;
			try { str = (new StreamReader(asyncParam.WebResponse.GetResponseStream())).ReadToEnd(); }
			catch { }
		    try { json = JObject.Parse(str); }
		    catch { }
			result.NumberOfCanonicalIds = json.Value<long>("canonical_ids");
			result.NumberOfFailures = json.Value<long>("failure");
			result.NumberOfSuccesses = json.Value<long>("success");
					
			var jsonResults = json["results"] as JArray;
			if (jsonResults == null)
				jsonResults = new JArray();
			foreach (var r in jsonResults)
			{
				var msgResult = new GcmMessageResult();
								
				msgResult.MessageId = r.Value<string>("message_id");
				msgResult.CanonicalRegistrationId = r.Value<string>("registration_id");
				msgResult.ResponseStatus = GcmMessageTransportResponseStatus.Ok;
				
				if (!string.IsNullOrEmpty(msgResult.CanonicalRegistrationId))
				{
					msgResult.ResponseStatus = GcmMessageTransportResponseStatus.CanonicalRegistrationId;
				}
				else if (r["error"] != null)
				{
					var err = r.Value<string>("error") ?? "";
					switch (err.ToLowerInvariant().Trim())
					{
						case "ok":
							msgResult.ResponseStatus = GcmMessageTransportResponseStatus.Ok;
							break;
						case "missingregistration":
							msgResult.ResponseStatus = GcmMessageTransportResponseStatus.MissingRegistrationId;
							break;
						case "unavailable":
							msgResult.ResponseStatus = GcmMessageTransportResponseStatus.Unavailable;
							break;
						case "notregistered":
							msgResult.ResponseStatus = GcmMessageTransportResponseStatus.NotRegistered;
							break;
						case "invalidregistration":
							msgResult.ResponseStatus = GcmMessageTransportResponseStatus.InvalidRegistration;
							break;
						case "mismatchsenderid":
							msgResult.ResponseStatus = GcmMessageTransportResponseStatus.MismatchSenderId;
							break;
						case "messagetoobig":
							msgResult.ResponseStatus = GcmMessageTransportResponseStatus.MessageTooBig;
							break;
                        case "invaliddatakey":
                            msgResult.ResponseStatus = GcmMessageTransportResponseStatus.InvalidDataKey;
                            break;
                        case "invalidttl":
                            msgResult.ResponseStatus = GcmMessageTransportResponseStatus.InvalidTtl;
                            break;
                        case "internalservererror":
                            msgResult.ResponseStatus = GcmMessageTransportResponseStatus.InternalServerError;
                            break;
						default:
							msgResult.ResponseStatus = GcmMessageTransportResponseStatus.Error;
							break;
					}
				}
				result.Results.Add(msgResult);
								
			}
			asyncParam.WebResponse.Close();
			int index = 0;
			var response = result;
			
			foreach (var r in response.Results)
			{
				var singleResultNotification = GcmNotification.ForSingleResult(response, index);
				if (r.ResponseStatus == GcmMessageTransportResponseStatus.Ok)
				{
					asyncParam.Callback(this, new SendNotificationResult(singleResultNotification));
				}
				else if (r.ResponseStatus == GcmMessageTransportResponseStatus.CanonicalRegistrationId)
				{
					var newRegistrationId = r.CanonicalRegistrationId;
					var oldRegistrationId = string.Empty;
					if (singleResultNotification.RegistrationIds != null && singleResultNotification.RegistrationIds.Count > 0)
						oldRegistrationId = singleResultNotification.RegistrationIds[0];
					asyncParam.Callback(this, new SendNotificationResult(singleResultNotification, false, new DeviceSubscriptonExpiredException()) { OldSubscriptionId = oldRegistrationId, NewSubscriptionId = newRegistrationId, IsSubscriptionExpired = true });
				}
				else if (r.ResponseStatus == GcmMessageTransportResponseStatus.Unavailable)
				{
					asyncParam.Callback(this, new SendNotificationResult(singleResultNotification, true, new Exception("Unavailable Response Status")));
				}
				else if (r.ResponseStatus == GcmMessageTransportResponseStatus.NotRegistered)
				{
					var oldRegistrationId = string.Empty;
					
					if (singleResultNotification.RegistrationIds != null && singleResultNotification.RegistrationIds.Count > 0)
						oldRegistrationId = singleResultNotification.RegistrationIds[0];
						
					asyncParam.Callback(this, new SendNotificationResult(singleResultNotification, false, new DeviceSubscriptonExpiredException()) { OldSubscriptionId = oldRegistrationId, IsSubscriptionExpired = true, SubscriptionExpiryUtc = DateTime.UtcNow });
				}
				else
				{
					asyncParam.Callback(this, new SendNotificationResult(singleResultNotification, false, new GcmMessageTransportException(r.ResponseStatus.ToString(), response)));
				}
				index++;
			}
			Interlocked.Decrement(ref waitCounter);
		}			
    }
