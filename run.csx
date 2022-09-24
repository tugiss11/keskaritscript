
#r "Newtonsoft.Json"
#r "System.Linq"

using Newtonsoft.Json;
using System.Linq;
using System;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

private static readonly HttpClient client = new HttpClient();

public class SignUp
{
        public int member1_id { get; set; }
        public string Name1 { get; set; }
        public int Group { get; set;}
        public double Ranking { get; set;}
}
public class Ranking
{
        public int id { get; set; }
        public string allpoints { get; set; }
        public string best6 { get; set; }
        public string firstname { get; set; }
        public string lastname { get; set; }
        public int paid { get; set; }
}

public class Event
{
    public int id { get; set;}
    public string name { get; set;}
}

public class Group
{
    public int id { get; set;}
}

private static async Task<int?> GetEventId(string name, ILogger log)
{
    var response = await client.GetAsync($"https://hbv.fi/hbv-api/events");
    var responseString = await response.Content.ReadAsStringAsync();
    var events = JsonConvert.DeserializeObject<List<Event>>(responseString);
    var eventId = events.FirstOrDefault(o => o.name.ToLower().Contains(name))?.id;
    if (eventId == null) { return null; }
    
    var response2 = await client.GetAsync($"https://hbv.fi/hbv-api/events/groups/{eventId}");
    var responseString2 = await response2.Content.ReadAsStringAsync();
    
    var groups = JsonConvert.DeserializeObject<List<Group>>(responseString2);
    return groups.FirstOrDefault().id;
}




public static async Task<IActionResult> Run(HttpRequest req, ILogger log)
{
    try{

    
    string queryValue = req.Query["event"];
    string requestShowAll = req.Query["showall"];
    foreach(var value in req.Query)
    {
         log.LogInformation(value.ToString());
    }
   
    var eventType =  !String.IsNullOrEmpty(queryValue) ? queryValue : "keskarit";

    var showAll =  !String.IsNullOrEmpty(requestShowAll) ? requestShowAll : "false";

    var id = await GetEventId(eventType, log);

    if (id == null) {  return (ActionResult)new OkObjectResult("No signups"); }

    log.LogInformation(eventType);
    if (eventType == "king") {
        if (showAll == "true") { return await KingAllAsync(id.Value, "keskarit");}
        return await KingAsync(id.Value, "keskarit");
    } else if (eventType == "queen")
    {
        if (showAll == "true") { return await KingAllAsync(id.Value, "tirsat");}
        return await KingAsync(id.Value, "tirsat");
    }
  
    var year = DateTime.Now.ToString("yyyy");
    var response = await client.GetAsync($"https://prod.hbv.fi/hbv-api/events/participants/{id}");
    var responseString = await response.Content.ReadAsStringAsync();
    var signUps = JsonConvert.DeserializeObject<List<SignUp>>(responseString);
    var rankingResponse = await client.GetAsync($"https://prod.hbv.fi/hbv-api/weekgameranking/points?serie={eventType}&year={year}");
    var rankingString = await rankingResponse.Content.ReadAsStringAsync();
    var rankings = JsonConvert.DeserializeObject<List<Ranking>>(rankingString);
    foreach (var signup in signUps)
    {
        var points = rankings.FirstOrDefault(o => o.id == signup.member1_id)?.allpoints;
        if (points != null) 
        {
            var pointsArray = points.Split(',').Reverse().ToArray();
            if (pointsArray.Length > 1)
            {
                signup.Ranking =  (Convert.ToDouble(pointsArray[0])+Convert.ToDouble(pointsArray[1]))/2;
            }
            else if (pointsArray.Length > 0) {
                signup.Ranking = Convert.ToDouble(pointsArray[0]);
            }
            else {
                signup.Ranking = 0;
            }
        }
        else {
            log.LogInformation($"no points found for {signup.Name1}");
            signup.Ranking = 0;
        }
    }
    var orderedsignUps = signUps.OrderByDescending(r => r.Ranking).ThenBy(o => o.member1_id);
    var group = 1;
    var count = 0;
    var result = new StringBuilder();
    result.AppendLine($"Group generated {DateTime.Now} UTC");
    result.AppendLine($"Number of players: {signUps.Count()}");
    foreach (var signup in orderedsignUps)
    {
        signup.Group = group;
        count++;
        result.AppendLine($"{signup.Group} {signup.Name1} {Math.Round(signup.Ranking, 2)}");
        if (count == 4) { group++; count = 0; result.AppendLine("");}
        //log.LogInformation($"{signup.Group} {signup.Name1} {signup.Ranking}");
       
    }
    return (ActionResult)new OkObjectResult(result.ToString());
    }

    catch (Exception ex)
    {
        log.LogInformation($"{ex.Message}");
        log.LogInformation($"{ex.StackTrace}");
        return (ActionResult)new OkObjectResult("Taking a break.");
    }
     
    
}

public static async Task<IActionResult> KingAsync(int id, string eventType)
{
    var year = DateTime.Now.ToString("yyyy");
    var response = await client.GetAsync($"https://hbv.fi/hbv-api/events/participants/{id}");
    var responseString = await response.Content.ReadAsStringAsync();
    var signUps = JsonConvert.DeserializeObject<List<SignUp>>(responseString);
    var rankingResponse = await client.GetAsync($"https://prod.hbv.fi/hbv-api/weekgameranking/points?serie={eventType}&year={year}");
    var rankingString = await rankingResponse.Content.ReadAsStringAsync();
    var rankings = JsonConvert.DeserializeObject<List<Ranking>>(rankingString);
    foreach (var signup in signUps)
    {
        var points = rankings.FirstOrDefault(o => o.id == signup.member1_id)?.allpoints ?? "0";
  
        var pointsArray = points.Split(',').OrderByDescending(o => Convert.ToDouble(o)).ToArray();
        var i= 0;
        foreach(var number in pointsArray)
        {
            signup.Ranking =  signup.Ranking + Convert.ToDouble(number);
            i++;
            if (i >= 6) { break;}
        }
        
    }
    var orderedsignUps = signUps.OrderByDescending(r => r.Ranking).ThenBy(o => o.member1_id);
    var group = 1;
    var count = 0;
    var result = new StringBuilder();
    result.AppendLine($"List generated {DateTime.Now} UTC");
    result.AppendLine($"Number of players: {signUps.Count()}");
    foreach (var signup in orderedsignUps)
    {
        signup.Group = group;
        count++;
        result.AppendLine($"{count}. {signup.Name1} {Math.Round(signup.Ranking, 2)}");
        if (count == 24) {  result.AppendLine("");}
        //log.LogInformation($"{signup.Group} {signup.Name1} {signup.Ranking}");
       
    }
    return (ActionResult)new OkObjectResult(result.ToString());
}


public static async Task<IActionResult> KingAllAsync(int id, string eventType)
{
    var year = DateTime.Now.ToString("yyyy");
    var response = await client.GetAsync($"https://prod.hbv.fi/hbv-api/events/participants/{id}");
    var responseString = await response.Content.ReadAsStringAsync();
    var signUps = JsonConvert.DeserializeObject<List<SignUp>>(responseString);
    var rankingResponse = await client.GetAsync($"https://prod.hbv.fi/hbv-api/weekgameranking/points?serie={eventType}&year={year}");
    var rankingString = await rankingResponse.Content.ReadAsStringAsync();
    var rankings = JsonConvert.DeserializeObject<List<Ranking>>(rankingString);
    var allPlayers = new List<SignUp>();
    foreach (var ranking in rankings.Where(o => o.paid == 1))
    {
        var points = ranking?.allpoints ?? "0";
        var signUp = new SignUp() {Name1 = ranking.firstname + " "+ ranking.lastname, member1_id = ranking.id};
        var pointsArray = points.Split(',').OrderByDescending(o => Convert.ToDouble(o)).ToArray();
        var i= 0;
        foreach(var number in pointsArray)
        {
            signUp.Ranking =  signUp.Ranking + Convert.ToDouble(number);
            i++;
            if (i >= 6) { break;}
        }
        allPlayers.Add(signUp);
    }
    var orderedsignUps = allPlayers.OrderByDescending(r => r.Ranking).ThenBy(o => o.member1_id);
    var group = 1;
    var count = 1;
    var result = new StringBuilder();
    result.AppendLine($"List generated {DateTime.Now} UTC");
    result.AppendLine($"Number of players: {signUps.Count()}");
    foreach (var signup in orderedsignUps)
    {
        signup.Group = group;
       
        if (signUps.FirstOrDefault(o => o.member1_id == signup.member1_id) != null)
        {
            if (count == 25) {  result.AppendLine("");}
            result.AppendLine($"{count}. {signup.Name1} {Math.Round(signup.Ranking, 2)}");
            count++;
        
        }
        else if (count < 30)
        {
            result.AppendLine($"  ({signup.Name1} {Math.Round(signup.Ranking, 2)})");
        }
        //log.LogInformation($"{signup.Group} {signup.Name1} {signup.Ranking}");
       
    }
    return (ActionResult)new OkObjectResult(result.ToString());
}




