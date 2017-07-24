﻿using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Query;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.Results;
using System.Xml;
using Microsoft.ServiceFabric.Services.Queryable.Controller;

namespace Microsoft.ServiceFabric.Services.Queryable
{
	public abstract class QueryableController : ApiController
	{
		protected async Task<IHttpActionResult> GetMetadataAsync(string application, string service)
		{
			var serviceUri = GetServiceUri(application, service);

			try
			{
				var proxy = await GetServiceProxyAsync<IQueryableService>(serviceUri).ConfigureAwait(false);
				var metadata = await proxy.GetMetadataAsync().ConfigureAwait(false);

				// Parse the metadata as xml.
				XmlDocument xml = new XmlDocument();
				xml.LoadXml(metadata);

				// Return xml response.
				var response =
					new HttpResponseMessage {Content = new StringContent(xml.InnerXml, Encoding.UTF8, "application/xml")};
				return new ResponseMessageResult(response);
			}
			catch (Exception e)
			{
				return HandleException(e, serviceUri);
			}
		}

		protected async Task<IHttpActionResult> QueryAsync(string application, string service, string collection)
		{
			var serviceUri = GetServiceUri(application, service);

			try
			{
				var query = Request.GetQueryNameValuePairs();

				// Query one service partition, allowing the partition to do the distributed query.
				var proxy = await GetServiceProxyAsync<IQueryableService>(serviceUri).ConfigureAwait(false);
				var results = await proxy.QueryAsync(collection, query).ConfigureAwait(false);

				// Construct the final, aggregated result.
				var result = new ODataResult
				{
					ODataMetadata = "",
					Value = results.Select(JsonConvert.DeserializeObject<JObject>),
				};

				return Ok(result);
			}
			catch (Exception e)
			{
				return HandleException(e, serviceUri);
			}
		}

		protected async Task<IHttpActionResult> DeleteAsync(string application, string service, string collection,
			ValueViewModel[] obj)
		{
			var serviceUri = GetServiceUri(application, service);
			try
			{

				Dictionary<Guid, List<JToken>> preMap = new Dictionary<Guid, List<JToken>>();

				Dictionary<Guid, Dictionary<JToken, bool>> parResult = new Dictionary<Guid, Dictionary<JToken, bool>>();

				for (int i = 0; i < obj.Length; i++)
				{
					List<JToken> templist = new List<JToken>();
					if (preMap.ContainsKey(obj[i].PartitionId))
					{
						templist = preMap[obj[i].PartitionId];
						templist.Add(obj[i].Key);
						preMap[obj[i].PartitionId] = templist;

					}
					else
					{
						templist.Add(obj[i].Key);
						preMap[obj[i].PartitionId] = templist;

					}
				}

				foreach (Guid mypid in preMap.Keys)
				{
					//Fetch partition proxy.
					var proxy = await GetServiceProxyForDeleteAsync<IQueryableService>(serviceUri, mypid).ConfigureAwait(false);
					Dictionary<JToken, bool> keyResult = new Dictionary<JToken, bool>();

					foreach (JToken myKey in preMap[mypid])
					{
						string keyquoted = JsonConvert.SerializeObject(myKey,
							new JsonSerializerSettings {StringEscapeHandling = StringEscapeHandling.EscapeNonAscii});
						keyResult[myKey] = await proxy.DeleteAsync(collection, keyquoted);

					}

					parResult[mypid] = keyResult;
				}

				return Ok(parResult);
			}
			catch (Exception e)
			{
				return HandleException(e, serviceUri);
			}
		}


		protected async Task<IHttpActionResult> AddAsync(string application, string service, string collection,
			ValueViewModel[] obj)
		{
			var serviceUri = GetServiceUri(application, service);
			try
			{
				bool[] results = new bool[obj.Length];
				Dictionary<JToken, bool> results1 = new Dictionary<JToken, bool>();

				/*Dictionary<Guid, List<int>> preMap = new Dictionary<Guid, List<int>>();
				
				for (int i = 0; i < obj.Length; i++)
				{
					List<int> templist = new List<int>();
					if (preMap.ContainsKey(obj[i].PartitionId))
					{
						templist = preMap[obj[i].PartitionId];
						templist.Add(i);
						preMap[obj[i].PartitionId] = templist;

					}
					else
					{
						templist.Add(i);
						preMap[obj[i].PartitionId] = templist;

					}
				}

				foreach (Guid mypid in preMap.Keys)
				{
					//Fetch partition proxy.
					var proxy = await GetServiceProxyForDeleteAsync<IQueryableService>(serviceUri, mypid).ConfigureAwait(false);
					Dictionary<JToken, bool> keyResult = new Dictionary<JToken, bool>();

					foreach (int k in preMap[mypid])
					{
						string keyquoted = JsonConvert.SerializeObject(obj[k].Key,
							new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii });
						//Serialize the value from the json body and put it into a string.
						string valuequoted = JsonConvert.SerializeObject(obj[k].Value,
							new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii });
						//keyResult[i] = await proxy.DeleteAsync(collection, keyquoted);

					}

					parResult[mypid] = keyResult;
				}

				return Ok(parResult);
				*/

				for (int i = 0; i < obj.Length; i++)
				{
					//Serialize the key from the json body and put it into a string.
					string keyquoted = JsonConvert.SerializeObject(obj[i].Key,
						new JsonSerializerSettings {StringEscapeHandling = StringEscapeHandling.EscapeNonAscii});
					//Serialize the value from the json body and put it into a string.
					string valuequoted = JsonConvert.SerializeObject(obj[i].Value,
						new JsonSerializerSettings {StringEscapeHandling = StringEscapeHandling.EscapeNonAscii});
					//Fetch the proxy
					var proxy = await GetServiceProxyForAddAsync<IQueryableService>(serviceUri, obj[i].PartitionId)
						.ConfigureAwait(false);
					results[i] = await proxy.AddAsync(collection, keyquoted, valuequoted).ConfigureAwait(false);
					results1[obj[i].Key] = results[i];

				}
				return Ok(results1);
			}
			catch (Exception e)
			{
				return HandleException(e, serviceUri);
			}

		}

		protected async Task<IHttpActionResult> UpdateAsync(string application, string service, string collection,
			ValueViewModel[] obj)
		{
			var serviceUri = GetServiceUri(application, service);
			try
			{

				bool[][] results = new bool[obj.Length][];
				Dictionary<JToken, bool[]> results1 = new Dictionary<JToken, bool[]>();
				for (int i = 0; i < obj.Length; i++)
				{
					//Serialize the key from the json body and put it into a string.
					string keyquoted = JsonConvert.SerializeObject(obj[i].Key,
						new JsonSerializerSettings {StringEscapeHandling = StringEscapeHandling.EscapeNonAscii});
					//Serialize the value from the json body and put it into a string.
					string valuequoted = JsonConvert.SerializeObject(obj[i].Value,
						new JsonSerializerSettings {StringEscapeHandling = StringEscapeHandling.EscapeNonAscii});

					var proxy = await GetServiceProxyForPartitionAsync<IQueryableService>(serviceUri, obj[i].PartitionId)
						.ConfigureAwait(false);
					results[i] = await Task.WhenAll(proxy.Select(p => p.UpdateAsync(collection, keyquoted, valuequoted)))
						.ConfigureAwait(false);
					results1[obj[i].Key] = results[i];

				}
				return Ok(results1);
			}
			catch (Exception e)
			{
				return HandleException(e, serviceUri);
			}

		}

		private IHttpActionResult HandleException(Exception e, Uri serviceUri)
		{
			if (e is FabricServiceNotFoundException)
				return Content(HttpStatusCode.NotFound, new {Message = $"Service '{serviceUri}' not found."});

			if (e is ArgumentException)
				return BadRequest(e.Message);
			if (e.InnerException is ArgumentException)
				return BadRequest(e.InnerException.Message);
			if (e is HttpException)
				return Content((HttpStatusCode) ((HttpException) e).GetHttpCode(), ((HttpException) e).Message);
			if (e.InnerException is HttpException)
				return Content((HttpStatusCode) ((HttpException) e.InnerException).GetHttpCode(),
					((HttpException) e.InnerException).Message);

			if (e is AggregateException)
				return InternalServerError(e.InnerException ?? e);

			return InternalServerError(e);
		}

		private static readonly ThreadLocal<Random> random = new ThreadLocal<Random>(() => new Random());

		private static async Task<T> GetServiceProxyAsync<T>(Uri serviceUri) where T : IService
		{
			using (var client = new FabricClient())
			{
				var partitions = await client.QueryManager.GetPartitionListAsync(serviceUri).ConfigureAwait(false);
				int randomindex = random.Value.Next(0, partitions.Count);
				return CreateServiceProxy<T>(serviceUri, partitions[randomindex]);
			}
		}

		private static async Task<IEnumerable<T>> GetServiceProxyForPartitionAsync<T>(Uri serviceUri, Guid partitionId)
			where T : IService
		{
			using (var client = new FabricClient())
			{
				var partitions = await client.QueryManager.GetPartitionListAsync(serviceUri).ConfigureAwait(false);
				var matchingPartitions =
					partitions.Where(p => p.PartitionInformation.Id == partitionId || partitionId == Guid.Empty);
				return matchingPartitions.Select(p => CreateServiceProxy<T>(serviceUri, p));
			}
		}

		private static async Task<T> GetServiceProxyForDeleteAsync<T>(Uri serviceUri, Guid partitionId)
			where T : IService
		{
			using (var client = new FabricClient())
			{
				var partitions = await client.QueryManager.GetPartitionListAsync(serviceUri).ConfigureAwait(false);
				var matchingPartition = partitions.Where(p => p.PartitionInformation.Id == partitionId );
				return  CreateServiceProxy<T>(serviceUri, matchingPartition.First());
			}
		}


		private static async Task<T> GetServiceProxyForAddAsync<T>(Uri serviceUri, Guid partitionId) where T : IService
		{
			using (var client = new FabricClient())
			{
				var partitions = await client.QueryManager.GetPartitionListAsync(serviceUri).ConfigureAwait(false);
				var matchingPartitions = partitions.Where(p => p.PartitionInformation.Id == partitionId);

				int randomindex = random.Value.Next(0, partitions.Count);

				if (partitionId == Guid.Empty)
				{
					return CreateServiceProxy<T>(serviceUri, partitions[randomindex]);
				}
				return CreateServiceProxy<T>(serviceUri, matchingPartitions.First());

			}
		}

		private static T CreateServiceProxy<T>(Uri serviceUri, Partition partition) where T : IService
		{
			if (partition.PartitionInformation is Int64RangePartitionInformation)
				return ServiceProxy.Create<T>(serviceUri,
					new ServicePartitionKey(((Int64RangePartitionInformation) partition.PartitionInformation).LowKey));
			if (partition.PartitionInformation is NamedPartitionInformation)
				return ServiceProxy.Create<T>(serviceUri,
					new ServicePartitionKey(((NamedPartitionInformation) partition.PartitionInformation).Name));
			if (partition.PartitionInformation is SingletonPartitionInformation)
				return ServiceProxy.Create<T>(serviceUri);

			throw new ArgumentException(nameof(partition));
		}

		private static Uri GetServiceUri(string applicationName, string serviceName)
		{
			var applicationUri = new Uri($"fabric:/{applicationName}/");
			return new Uri(applicationUri, serviceName);
		}
	}
}
