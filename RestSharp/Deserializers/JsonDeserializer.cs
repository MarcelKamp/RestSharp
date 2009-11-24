﻿#region License
//   Copyright 2009 John Sheehan
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License. 
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace RestSharp.Deserializers
{
	public class JsonDeserializer : IDeserializer
	{
		public string RootElement { get; set; }
		public string Namespace { get; set; }
		public DateFormat DateFormat { get; set; }

		public X Deserialize<X>(string content) where X : new() {
			var x = new X();

			JObject json = JObject.Parse(content);
			JToken root = json.Root;

			if (RootElement.HasValue())
				root = json[RootElement];

			Map(x, root);

			return x;
		}

		private void Map(object x, JToken json) {
			var objType = x.GetType();
			var props = objType.GetProperties();

			foreach (var prop in props) {
				var type = prop.PropertyType;

				if (!type.IsPublic || !prop.CanWrite)
					continue;

				var name = prop.Name;
				var value = json[name];
				var actualName = name;

				if (value == null) {
					// try camel cased name
					actualName = name.ToCamelCase();
					value = json[actualName];
				}

				if (value == null) {
					// try lower cased name
					actualName = name.ToLower();
					value = json[actualName];
				}

				if (value == null) {
					// try name with underscores
					actualName = name.AddUnderscores();
					value = json[actualName];
				}

				if (value == null) {
					// try name with underscores with lower case
					actualName = name.AddUnderscores().ToLower();
					value = json[actualName];
				}

				if (value == null)
					continue;

				if (type.IsPrimitive) {
					prop.SetValue(x, Convert.ChangeType(value.ToString(), type), null);
				}
				else if (type == typeof(string)) {
					string raw = value.ToString();
					// remove leading and trailing "
					prop.SetValue(x, raw.Substring(1, raw.Length - 2), null);
				}
				else if (type == typeof(DateTime)) {
					var dt = value != null ? value.ToString().ParseJsonDate() : default(DateTime);
					prop.SetValue(x, dt, null);
				}
				else if (type == typeof(Decimal)) {
					var dec = value != null ? Decimal.Parse(value.ToString()) : default(decimal);
					prop.SetValue(x, dec, null);
				}
				else if (type.IsGenericType) {
					var genericTypeDef = type.GetGenericTypeDefinition();
					if (genericTypeDef == typeof(List<>)) {
						var t = type.GetGenericArguments()[0];
						var list = (IList)Activator.CreateInstance(type);

						var elements = value.Children();
						foreach (var element in elements) {
							var item = CreateAndMap(t, element);
							list.Add(item);
						}

						prop.SetValue(x, list, null);
					}
					else if (genericTypeDef == typeof(Dictionary<,>)) {
						var genericArgs = type.GetGenericArguments();
						var keyType = genericArgs[0];

						// only supports Dict<string, T>()
						if (keyType == typeof(string)) {
							var valueType = genericArgs[1];

							var dict = (IDictionary)Activator.CreateInstance(type);
							var elements = value.Children();
							foreach (JProperty element in elements) {
								var key = element.Name;
								var item = CreateAndMap(valueType, element.Value);
								dict.Add(key, item);
							}

							prop.SetValue(x, dict, null);
						}
					}
				}
				else {
					// nested property classes
					var item = CreateAndMap(type, json[actualName]);
					prop.SetValue(x, item, null);
				}
			}
		}

		private object CreateAndMap(Type t, JToken element) {
			var item = Activator.CreateInstance(t);
			Map(item, element);
			return item;
		}

	}
}