﻿using Miki.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using Miki.Exceptions;

namespace Miki
{
	public class WebhookManager
	{
		public static event Func<WebhookResponse, Task> OnEvent;

		private static IConnection connection;
		private static IModel channel;

		public static void Listen(string v)
		{
			ConnectionFactory factory = new ConnectionFactory();
			factory.Uri = Global.Config.RabbitUrl;

			connection = factory.CreateConnection();
			channel = connection.CreateModel();
			channel.ExchangeDeclare("miki", ExchangeType.Direct, true);
			channel.QueueDeclare(v, true, false, false, null);
			channel.QueueBind(v, "miki", "*", null);

			var consumer = new EventingBasicConsumer(channel);
			consumer.Received += async (ch, ea) =>
			{
				WebhookResponse resp = null;
				try
				{
					string payload = Encoding.UTF8.GetString(ea.Body);
					resp = JsonConvert.DeserializeObject<WebhookResponse>(payload);
				}
				catch(Exception e)
				{
					Log.Error(e);
					channel.BasicReject(ea.DeliveryTag, false);
					return;
				}

				if (OnEvent.GetInvocationList().Length > 0)
				{
					try
					{
						await OnEvent(resp);
						channel.BasicAck(ea.DeliveryTag, false);
					}
					catch (RabbitException e)
					{
						var prop = channel.CreateBasicProperties();
						prop.Headers = new Dictionary<string, object>();

						if (ea.BasicProperties.Headers.TryGetValue("x-retry-count", out object value))
						{
							int rCount = int.Parse(value.ToString() ?? "0") + 1;

							prop.Headers.Add("x-retry-count", rCount);

							if (rCount > 10)
							{
								return;
							}
						}
						else
						{
							prop.Headers.Add("x-retry-count", 1);
						}
						channel.BasicPublish("miki", "*", false, prop, ea.Body);
					}
					catch (Exception e)
					{
						Log.Error(e);
						channel.BasicAck(ea.DeliveryTag, false);
					}
				}
			};
			string consumerTag = channel.BasicConsume("", false, consumer);
		}
	}

	public class WebhookResponse
	{
		[JsonProperty("auth_code")]
		public string auth_code;

		[JsonProperty("data")]
		public JToken data;
	}
}
