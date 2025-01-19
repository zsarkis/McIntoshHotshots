using System;
using System.Threading.Tasks;
using PuppeteerSharp;

namespace McIntoshHotshots.Services;

public interface IDartConnectReportParsingService
{
    Task ParseDartConnectReport();
}

public class DartConnectReportParsingService : IDartConnectReportParsingService
{
    //TODO: re-write this in python and stick it in a lambda
    public async Task ParseDartConnectReport()
    {
        var url = "https://recap.dartconnect.com/games/6785d51af63413a02a9be35b";
        
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
        await page.GoToAsync(url);

        Thread.Sleep(500);
        
        // Select all elements where the class contains "turn_stats"
        var elements = await page.QuerySelectorAllAsync("[class*='turn_stats']");

        var parsedData = new List<Dictionary<string, string>>();

        int rowCount = 0;
        bool runAfter = false;
                
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