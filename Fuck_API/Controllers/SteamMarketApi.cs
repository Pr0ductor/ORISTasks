using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;

namespace SteamMarketApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MarketController : ControllerBase
    {
        private readonly HttpClient _httpClient;

        public MarketController(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        [HttpGet]
        public async Task<IActionResult> GetItemsForHeroes([FromQuery] List<string> heroes, int pageNumber = 1, int pageSize = 10)
        {
            // Проверяем, что переданы именно нужные герои по заданию из классной работы
            var validHeroes = new List<string> { "terrorblade", "viper", "wisp" };
            if (heroes == null || !heroes.All(hero => validHeroes.Contains(hero)))
            {
                return BadRequest("Неверный список героев. Допустимые герои: terrorblade, viper, wisp.");
            }

            var result = new Dictionary<string, List<Card>>();

            foreach (var hero in heroes)
            {
                var items = await ParseHeroItems(hero, pageNumber, pageSize);
                result[hero] = items;
            }

            return Ok(result);
        }

        private async Task<List<Card>> ParseHeroItems(string hero, int pageNumber, int pageSize)
        {
            var cards = new List<Card>();
            var totalItemsNeeded = pageNumber * pageSize;
            var pagesPerHero = (int)Math.Ceiling((double)totalItemsNeeded / 10); // 10 элементов на странице

            for (var page = 1; page <= pagesPerHero; page++)
            {
                var url = page == 1
                    ? $"https://steamcommunity.com/market/search?q=&category_570_Hero%5B%5D=tag_npc_dota_hero_{hero.ToLower()}&category_570_Slot%5B%5D=any&category_570_Type%5B%5D=any&appid=570"
                    : $"https://steamcommunity.com/market/search?q=&category_570_Hero%5B%5D=tag_npc_dota_hero_{hero.ToLower()}&category_570_Slot%5B%5D=any&category_570_Type%5B%5D=any&appid=570#p{page}_popular_desc";

                try
                {
                    Console.WriteLine($"Запрос к URL: {url}");

                    // Загружаем HTML-страницу
                    var html = await _httpClient.GetStringAsync(url);
                    Console.WriteLine($"Получен HTML для героя {hero} (страница {page}): {html.Substring(0, 500)}...");

                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);

                    // Парсим товары
                    var cardNodes = doc.DocumentNode.SelectNodes("//div[contains(@id, 'result_') and not(contains(@id, 'resultlink_'))]");
                    if (cardNodes == null)
                    {
                        Console.WriteLine($"Не найдены товары для героя {hero} (страница {page})");
                        continue;
                    }

                    foreach (var node in cardNodes.Take(pageSize))
                    {
                        var id = node.Id.Split('_')[1];
                        var nameNode = node.SelectSingleNode($".//span[@id='result_{id}_name']");
                        var imageNode = node.SelectSingleNode($".//*[@id='result_{id}_image']");
                        var gameNode = node.SelectSingleNode(".//span[contains(@class, 'market_listing_game_name')]");
                        var priceNode = node.SelectSingleNode(".//span[contains(@class, 'market_table_value')]/span[contains(@class, 'normal_price')]");
                        var amountNode = node.SelectSingleNode(".//span[contains(@class, 'market_listing_num_listings')]");

                        // Проверяем, что все необходимые элементы найдены
                        if (nameNode == null || imageNode == null || gameNode == null || priceNode == null || amountNode == null)
                        {
                            Console.WriteLine($"Не удалось найти все элементы для товара с ID {id} (герой {hero}, страница {page})");
                            continue;
                        }

                        var card = new Card
                        {
                            Name = nameNode.InnerText.Trim(),
                            ImageUrl = imageNode.Attributes["src"]?.Value,
                            Game = gameNode.InnerText.Trim(),
                            Price = priceNode.InnerText.Trim(),
                            Amount = amountNode.InnerText.Trim(),
                            Hero = hero
                        };

                        Console.WriteLine($"Добавлен товар: {card.Name}, Цена: {card.Price}, Количество: {card.Amount}");

                        cards.Add(card);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при парсинге данных для героя {hero}: {ex.Message}");
                }

                // Добавляем задержку между запросами
                await Task.Delay(2000); // 2 секунды
            }

            // Перемешиваем результаты
            var rnd = new Random();
            return cards.OrderBy(x => rnd.Next()).Take(pageSize).ToList();
            // В самом сваггере нужно указать героев чере heroes array, в принципе работает со всеми героями,
            // можно просто убрать условие сверху, что только 3 героя можно использовать
            //pagenumber можно указать любой, но шмоток на viper и io мало, поэтому будет выводить почти одно и то же
            //pagesize берёт количество шмоток на каждого героя(количество со страницы)
            // и самое главное стоит delay 2 секунды поэтому может грузить несколько секунд, но а так всё работает
            // даже в самом райдере можно перейти по ссылке и всё проверить
            
        }
    }

    public class Card
    {
        public string Name { get; set; }
        public string ImageUrl { get; set; }
        public string Game { get; set; }
        public string Price { get; set; }
        public string Amount { get; set; }
        public string Hero { get; set; }
    }
}