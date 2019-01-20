using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace orders
{
  public class Program
  {
    public static void Main(string[] args)
    {
      var parsedOrders = Task.Run(async () => await OrderSnapshot<PizzaOrder>("http://files.olo.com/pizzas.json"));
      Task.WaitAll(parsedOrders);

      var rank = 1;
      foreach (var order in CountOrders(parsedOrders.Result).Top(20))
      {
        Console.WriteLine($"Rank: {rank}, Pizza: {string.Join(",", order.Key.Toppings)}, Times ordered: {order.Value}");
        rank++;
      }
      Console.ReadLine();
    }

    private static Dictionary<string, KeyValuePair<PizzaOrder, int>> CountOrders(IEnumerable<PizzaOrder> parsedOrders)
    {
      var uniqueOrders = new Dictionary<string, KeyValuePair<PizzaOrder, int>>();
      foreach (var order in parsedOrders)
      {
        var sortedToppings = string.Join(",", (order.Toppings ?? new string[]{}).Select(a => a.ToUpperInvariant()).OrderBy(z => z));
        if (uniqueOrders.ContainsKey(sortedToppings))
        {
          var (key, value) = uniqueOrders[sortedToppings];
          uniqueOrders[sortedToppings] = new KeyValuePair<PizzaOrder, int>(key, value + 1);
          continue;
        }
        uniqueOrders.Add(sortedToppings, new KeyValuePair<PizzaOrder, int>(order, 1));
      }
      return uniqueOrders;
    }

    private static async Task<IEnumerable<T>> OrderSnapshot<T>(string orderUrl) where T : class, new()
    {
      using (var client = new HttpClient())
      {
        return await client.GetDataAsync<T>(orderUrl);
      }
    }
  }

  public static class DictionaryExtensions
  {
    public static Dictionary<T, int> Top<T>(this Dictionary<string, KeyValuePair<T, int>> uniqueOrders, int topN) where T : class, new()
    {
      var results = uniqueOrders.OrderByDescending(z => z.Value.Value)
        .Take(topN)
        .ToDictionary(x => x.Value.Key, x => x.Value.Value);
      return results;
    }
  }

  public static class HttpClientExtensions
  {
    private static IEnumerable<T> Read<T>(JsonReader reader) where T : class, new()
    {
      var items = new List<T>();
      reader.SupportMultipleContent = true;
      var serializer = new JsonSerializer();
      while (reader.Read())
      {
        if (reader.TokenType != JsonToken.StartObject) { continue; }
        items.Add(serializer.Deserialize<T>(reader));
      }
      return items;
    }

    public static async Task<IEnumerable<T>> GetDataAsync<T>(this HttpClient current, string orderUrl) where T : class, new()
    {
      using (var stream = await current.GetStreamAsync(orderUrl))
      using (var streamReader = new StreamReader(stream))
      using (var reader = new JsonTextReader(streamReader))
      {
        return Read<T>(reader);
      }
    }
  }

  [DataContract]
  public class PizzaOrder
  {
    [DataMember(Name = "toppings")]
    public string[] Toppings { get; set; }
  }
}
