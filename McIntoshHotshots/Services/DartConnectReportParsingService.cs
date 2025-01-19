using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using McIntoshHotshots.Model;
using PuppeteerSharp;
using McIntoshHotshots.Repo;

namespace McIntoshHotshots.Services;

public interface IDartConnectReportParsingService
{
    Task ParseDartConnectMatchFromReport(string url, int homePlayerId, int awayPlayerId);
    Task ParseDartConnectLegDetailFromReport(string url);
}

public class DartConnectReportParsingService : IDartConnectReportParsingService
{

    IPlayerRepo _playerRepo;
    
    public DartConnectReportParsingService(IPlayerRepo playerRepo)
    {
        _playerRepo = playerRepo;
    }
    //TODO: re-write this in python and stick it in a lambda
    public async Task ParseDartConnectMatchFromReport(string url, int homePlayerId, int awayPlayerId)
    {
        var homePlayer = await _playerRepo.GetPlayerByIdAsync(homePlayerId);
        var awayPlayer = await _playerRepo.GetPlayerByIdAsync(awayPlayerId);
        
        string updatedUrl = Regex.Replace(url, @"(?<=recap\.dartconnect\.com/)games", "matches");var browserFetcher = new BrowserFetcher();
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
                parsedData.Add(column3Content);
            }
        }

        // Print the parsed Column 3 data
        Console.WriteLine("Match Length");
        foreach (var value in parsedData)
        {
            Console.WriteLine(value);
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

            // Print the parsed data for debugging
            foreach (var row in parsedTableData)
            {
                //2nd (0 enum) row has a player name + legs won + avg (cols 1 + 3 + 9)
                //3rd (0 enum) row has a player name + legs won + avg (cols 1 + 3 + 9)
                //get player name and compare with player object name/email and find best fit for home/away assignments
                Console.WriteLine("Row:");
                foreach (var kvp in row)
                {
                    Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                }

                Console.WriteLine("--------------------");
            }
        }

    
        await browser.CloseAsync();
        
        //TODO: return match_id after it has been created
    }
    
    public async Task ParseDartConnectLegDetailFromReport(string url)
    {
        //TODO: pass in match ID so that you can update the cork winner PID

        string updatedUrl = Regex.Replace(url, @"(?<=recap\.dartconnect\.com/)matches", "games");
        // Download Chromium manually with a specific revision
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
        var elements = await page.QuerySelectorAllAsync("[class*='turn_stats']");

        var parsedData = new List<Dictionary<string, string>>();

        int rowCount = 0;
        bool runAfter = false;
        int gameCount = 0;
        
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
                    if (rowCount == 0 && !string.IsNullOrEmpty(className) && className.Contains("!bg-[green]"))
                    {
                        if (i == 3)
                        {
                            rowData[$"Column 2"] = rowData[$"Column 2"] + " Started!";
                        }
                        if (i == 5)
                        {
                            runAfter = true;
                        }
                    }

                    
                    // Use column indexes or infer based on class
                    rowData[$"Column {i + 1}"] = cellContent;
                }

                if (runAfter)
                {
                    rowData[$"Column 8"] = rowData[$"Column 8"] + " Started!";
                }
                
                if (int.TryParse(rowData[$"Column 3"].Trim(), out int col3) &&
                    int.TryParse(rowData[$"Column 4"].Trim(), out int col4) &&
                    int.TryParse(rowData[$"Column 6"].Trim(), out int col6) &&
                    int.TryParse(rowData[$"Column 7"].Trim(), out int col7))
                {
                    if (col3 + col4 == 501 && col6 + col7 == 501)
                    {
                        ++gameCount;
                    }
                }
                rowData[$"Game"] = gameCount.ToString();
                parsedData.Add(rowData);
            }
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
        
        await browser.CloseAsync();

    }
}