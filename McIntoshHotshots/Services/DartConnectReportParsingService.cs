using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using McIntoshHotshots.Model;
using PuppeteerSharp;
using McIntoshHotshots.Repo;

namespace McIntoshHotshots.Services;

public interface IDartConnectReportParsingService
{
    Task<int>  ParseDartConnectMatchFromReport(string url, int homePlayerId, int awayPlayerId);
    Task ParseDartConnectLegWithDetailFromReport(MatchSummaryModel matchSummary);
}

public class DartConnectReportParsingService : IDartConnectReportParsingService
{

    IPlayerRepo _playerRepo;
    IMatchSummaryRepo _matchSummaryRepo;
    ILegRepo _legRepo;
    
    public DartConnectReportParsingService(IPlayerRepo playerRepo, IMatchSummaryRepo matchSummaryRepo, ILegRepo legRepo)
    {
        _playerRepo = playerRepo;
        _matchSummaryRepo = matchSummaryRepo;
        _legRepo = legRepo;
    }
    
    //TODO: re-write this in python and stick it in a lambda
    public async Task<int> ParseDartConnectMatchFromReport(string url, int homePlayerId, int awayPlayerId)
    {
        var homePlayer = await _playerRepo.GetPlayerByIdAsync(homePlayerId);
        var awayPlayer = await _playerRepo.GetPlayerByIdAsync(awayPlayerId);
        var matchSummary = new MatchSummaryModel();
        
        matchSummary.UrlToRecap = url;
        matchSummary.HomePlayerId = homePlayerId;
        matchSummary.AwayPlayerId = awayPlayerId;
        
        string updatedUrl = Regex.Replace(url, @"(?<=recap\.dartconnect\.com/)games", "matches");
        var browserFetcher = new BrowserFetcher();
        await browserFetcher.DownloadAsync();

        // Launch Puppeteer
        using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true
        });
        using var page = await browser.NewPageAsync();

        // Navigate to the page
        await page.GoToAsync(updatedUrl);

        Thread.Sleep(500);
        
        // Select all elements where the class contains "turn_stats"
        var elements = await page.QuerySelectorAllAsync("[class*='matchHeaderStats']");

        var parsedData = new List<string>(); // Store only Column 3 values

        foreach (var row in elements)
        {
            var cells = await row.QuerySelectorAllAsync("td");
            if (cells.Length > 2)  // Ensure there are at least 3 columns
            {
                // Extract content from Column 3 (index 2)
                var column3Content = await cells[2].EvaluateFunctionAsync<string>("el => el.textContent.trim()");
                matchSummary.TimeElapsed = column3Content;
            }
        }

        //TODO: get the rest out from the table under the digital steel banner

        var table = await page.QuerySelectorAsync("table.w-full.border.border-\\[\\#666\\]");
        
        if (table != null)
        {
            var rows = await table.QuerySelectorAllAsync("tbody tr");
            var parsedTableData = new List<Dictionary<string, string>>();

            foreach (var row in rows)
            {
                var cells = await row.QuerySelectorAllAsync("td");
                var rowData = new Dictionary<string, string>();

                for (int i = 0; i < cells.Length; i++)
                {
                    var cellContent = await cells[i].EvaluateFunctionAsync<string>("el => el.textContent.trim()");
                    rowData[$"Column {i + 1}"] = cellContent;
                }

                parsedTableData.Add(rowData);
            }

            int rowCount = 0;
            // Print the parsed data for debugging
            foreach (var row in parsedTableData)
            {
                if (rowCount == 2)
                {
                    //TODO: Could be a corner case where players with the same last name match up, just try to make sure you assign the correct players to home/away
                    if (homePlayer.Name.Contains(row["Column 1"]) && !awayPlayer.Name.Contains(row["Column 1"]))
                    {
                        matchSummary.HomePlayerId = homePlayerId;
                        matchSummary.AwayPlayerId = awayPlayerId;
                        matchSummary.HomeLegsWon = Int32.Parse(row["Column 3"]);
                        matchSummary.HomeSetAverage = Double.Parse(row["Column 9"]);
                    }
                    else if (awayPlayer.Name.Contains(row["Column 1"]) && !homePlayer.Name.Contains(row["Column 1"]))
                    {
                        matchSummary.HomePlayerId = awayPlayerId;
                        matchSummary.AwayPlayerId = homePlayerId;
                        matchSummary.HomeLegsWon = Int32.Parse(row["Column 3"]);
                        matchSummary.HomeSetAverage = Double.Parse(row["Column 9"]);
                    }
                }

                if (rowCount == 3 && matchSummary is { AwayLegsWon: 0, AwaySetAverage: 0 })
                {
                    matchSummary.AwayLegsWon = Int32.Parse(row["Column 3"]);
                    matchSummary.AwaySetAverage = Double.Parse(row["Column 9"]);
                }
                //2nd (0 enum) row has a player name + legs won + avg (cols 1 + 3 + 9)
                //3rd (0 enum) row has a player name + legs won + avg (cols 1 + 3 + 9)
                //get player name and compare with player object name/email and find best fit for home/away assignments
                rowCount++;
            }
        }

        await browser.CloseAsync();

        var matchId = await _matchSummaryRepo.InsertMatchSummaryAsync(matchSummary);
        
        return matchId;
    }

    public async Task ParseDartConnectLegWithDetailFromReport(MatchSummaryModel matchSummary)
    {
        //TODO: new up leg
        string updatedUrl = Regex.Replace(matchSummary.UrlToRecap, @"(?<=recap\.dartconnect\.com/)matches", "games");
        // Download Chromium manually with a specific revision
        var browserFetcher = new BrowserFetcher();
        await browserFetcher.DownloadAsync();

        // Launch Puppeteer
        using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            ProtocolTimeout = 300000
        });
        using var page = await browser.NewPageAsync();

        // Navigate to the page
        await page.GoToAsync(updatedUrl, new NavigationOptions
        {
            Timeout = 300000,  // Increase timeout to 120 seconds
            WaitUntil = new[] { WaitUntilNavigation.Load }
        });
        
        await Task.Delay(5000);  // Wait for 5 seconds
        
        // Select elements with the class 'turn_stats' and also rows with class 'bg-[#eeeeee]'
        var elements = await page.QuerySelectorAllAsync("tr[class*='turn_stats'], tr[class='bg-\\[\\#eeeeee\\]'], tr[class='text-xl bg-\\[\\#434343\\] text-white']");

        //if you grab every row where the middle col says 3 Dart Avg and determine who started the leg (via green marker)
        //you can sort out how many darts home + away threw
        //can grab the rest from the leg number indicator? might be easier to lump this in with the detail

        var parsedData = new List<Dictionary<string, string>>();

        int rowCount = 0;
        bool runAfter = false;
        int gameCount = 0;
        LegModel currentLeg = new LegModel();
        
        foreach (var row in elements)
        {
            var cells = await row.QuerySelectorAllAsync("td");
            if (cells.Length > 0)
            {
                // Parse each cell in the row
                var rowData = new Dictionary<string, string>();
                
                for (int i = 0; i < cells.Length; i++)
                {
                    var cellContent = await cells[i].EvaluateFunctionAsync<string>("el => el.textContent.trim()");
                    var className = await cells[i].EvaluateFunctionAsync<string>("el => el.getAttribute('class')");
                    if (rowCount == 2 && !string.IsNullOrEmpty(className) && className.Contains("!bg-[green]"))
                    {
                        if (i == 3)
                        {
                            var homePlayer = await _playerRepo.GetPlayerByIdAsync(matchSummary.HomePlayerId);
                            var awayPlayer = await _playerRepo.GetPlayerByIdAsync(matchSummary.AwayPlayerId);
                            if (rowData[$"Column 2"].Contains(homePlayer.Name) || homePlayer.Name.Contains(rowData[$"Column 2"]))
                            {
                                matchSummary.CorkWinnerPlayerId = homePlayer.Id;
                                await _matchSummaryRepo.UpdateMatchSummaryAsync(matchSummary);
                            }
                            else if (rowData[$"Column 2"].Contains(awayPlayer.Name) ||
                                     awayPlayer.Name.Contains(rowData[$"Column 2"]))
                            {
                                matchSummary.CorkWinnerPlayerId = awayPlayer.Id;
                                await _matchSummaryRepo.UpdateMatchSummaryAsync(matchSummary);
                            }
                            rowData[$"Column 2"] = rowData[$"Column 2"] + " Started!";
                        }
                        if (i == 5)
                        {
                            runAfter = true;
                        }
                    }
                    
                    // Use column indexes or infer based on class
                    if (rowCount == 47)
                    {
                        Console.WriteLine("Processing row before issues.");
                        Console.WriteLine($"Row count: {rowCount}");
                        Console.WriteLine($"Column index: {i + 1}");
                        Console.WriteLine($"Cell content length: {cellContent.Length}");
                        Console.WriteLine($"Cell content: '{cellContent}'");
                    }
                    rowData[$"Column {i + 1}"] = cellContent;
                }

                if (rowCount == 47)
                {
                    Console.WriteLine("fffffff.");
                }

                if (runAfter)
                {
                    rowData[$"Column 8"] = rowData[$"Column 8"] + " Started!";
                }

                if (rowData["Column 1"].EndsWith("501 SIDO"))
                {
                    currentLeg.TimeElapsed = rowData["Column 5"];
                    if (rowData[$"Column 2"] == "0")
                    {
                        currentLeg.WinnerId = matchSummary.HomePlayerId;
                        currentLeg.LoserScoreRemaining = Int32.Parse(rowData[$"Column 4"]);
                    }                   
                    else if (rowData[$"Column 4"] == "0")
                    {
                        currentLeg.WinnerId = matchSummary.AwayPlayerId;
                        currentLeg.LoserScoreRemaining = Int32.Parse(rowData[$"Column 2"]);
                    }
                
                    currentLeg.MatchId = matchSummary.Id;
                }
                
                bool containsDo = false;
                string pattern = @"DO \((\d+)\)";
                
                if (rowData.TryGetValue("Column 9", out string column9Value) && Regex.IsMatch(column9Value, pattern))
                {
                    containsDo = true;
                }
                
                if (rowData.TryGetValue("Column 1", out string column1Value) && Regex.IsMatch(column1Value, pattern))
                {
                    containsDo = true;
                }
                
                if (containsDo)
                {
                    if (rowData[$"Column 4"] == "0")
                    {
                        var match1 = Regex.Match(rowData[$"Column 1"], @"\((\d+)\)");
                        currentLeg.WinnerId = matchSummary.HomePlayerId;
                        currentLeg.HomePlayerDartsThrown = ((Int32.Parse(rowData[$"Column 5"]) - 1) * 3) + (3 - Int32.Parse(match1.Groups[1].Value));
                        if (String.IsNullOrEmpty(rowData[$"Column 4"]))
                        {
                            currentLeg.AwayPlayerDartsThrown = (Int32.Parse(rowData[$"Column 5"]) - 1) * 3;
                        }
                        else
                        {
                            currentLeg.AwayPlayerDartsThrown = (Int32.Parse(rowData[$"Column 5"])) * 3;
                        }
                    }                   
                    else if (rowData[$"Column 6"] == "0")
                    {
                        var match9 = Regex.Match(rowData[$"Column 9"], @"\((\d+)\)");
                        currentLeg.WinnerId = matchSummary.AwayPlayerId;
                        currentLeg.AwayPlayerDartsThrown = ((Int32.Parse(rowData[$"Column 5"]) - 1) * 3) + Int32.Parse(match9.Groups[1].Value);
                        if (String.IsNullOrEmpty(rowData[$"Column 6"]))
                        {
                            currentLeg.HomePlayerDartsThrown = (Int32.Parse(rowData[$"Column 5"]) - 1) * 3;
                        }
                        else
                        {
                            currentLeg.HomePlayerDartsThrown = (Int32.Parse(rowData[$"Column 5"])) * 3;
                        }
                    }
                }
                
                if (int.TryParse(rowData[$"Column 3"].Trim(), out int col3) &&
                    int.TryParse(rowData[$"Column 4"].Trim(), out int col4) &&
                    int.TryParse(rowData[$"Column 6"].Trim(), out int col6) &&
                    int.TryParse(rowData[$"Column 7"].Trim(), out int col7))
                {
                    if (col3 + col4 == 501 && col6 + col7 == 501)
                    {
                        ++gameCount;
                        parsedData.Last()["Game"] = gameCount.ToString();
                        await _legRepo.InsertLegAsync(currentLeg);
                        currentLeg = new LegModel();
                        currentLeg.LegNumber = gameCount;
                    }
                }
                rowData[$"Game"] = gameCount.ToString();
                parsedData.Add(rowData);
            }
            Console.WriteLine("row: " + rowCount.ToString());
            rowCount++;
        }
        
        // Print the parsed data in a readable format
        foreach (var row in parsedData)
        {
            Console.WriteLine("Row:");
            foreach (var kvp in row)
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
            }
            Console.WriteLine("--------------------");
        }
        //TODO: new up legs
        //TODO: new up leg details
        
        await browser.CloseAsync();
    }
}