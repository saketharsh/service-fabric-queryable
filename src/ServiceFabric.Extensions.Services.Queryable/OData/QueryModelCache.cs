﻿using System;
using System.Collections.Concurrent;
using System.Web.Http.OData;
using System.Web.Http.OData.Builder;

namespace ServiceFabric.Extensions.Services.Queryable
{
	internal sealed class QueryModelCache
	{
		private static readonly ConcurrentDictionary<Type, ODataQueryContext> ContextCache =
			new ConcurrentDictionary<Type, ODataQueryContext>();

		public ODataQueryContext GetQueryContext(Type type)
		{
			return ContextCache.GetOrAdd(type, CreateQueryContext);
		}

		private static ODataQueryContext CreateQueryContext(Type type)
		{
			var builder = new ODataConventionModelBuilder();
			builder.AddEntity(type);
			var model = builder.GetEdmModel();
			return new ODataQueryContext(model, type);
		}
	}
}