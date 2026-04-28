// FOR BUILDING UI LIKE BUTTONS, TEXTBOXES 
using Avalonia.Controls;
// FOR ALIGNMENT AND SPACING 
using Avalonia.Layout;
// FOR HANDLING BUTTON CLICKS AND EVENTS
using Avalonia.Interactivity;
// FOR EXCEPTIONS
using System;
// FOR IMPORTING CLASSES LIKE LIST, DICTIONARY, AND STACK
using System.Collections.Generic;
// FOR FILE HANDLING TOOLS FOR READING/WRITING THE JSON FILES
using System.IO;
// IMPORTING LINQ FOR LIST-BASED OPERATIONS 
using System.Linq;
// FOR .NET NETWORK TOOLS
using System.Net;
// FOR WEB COMMUNICATION
using System.Net.Http;
// FOR USING JSON FOR SAVING/LOADING CONFIGURATIONS FOR HOMEPAGE, HISTORY AND BOOKMARKS
using System.Text.Json;
// FOR RECOGNISING TITLES AND LINKS FROM HTML WITH PATTERN MATCHING
using System.Text.RegularExpressions;
// FOR ASYNC/AWAIT
using System.Threading.Tasks;
// IMPORTING AVALONIA BASE FOR BASIC UI FUNCTIONALITY
using Avalonia;

namespace SimpleBrowser
{
    // MAIN BROWSER WINDOW CLASS
    public partial class MainWindow : Window
    {
        // HTTP CLIENT FOR SENDING AND RECEIVING WEB REQUESTS
        private readonly HttpClient _httpClient = new HttpClient();
        // FILE PATH FOR STORING HOMEPAGE CONFIGURATION
        private readonly string _configPath = "config.json";
        // FILE PATH FOR STORING BOOKMARK CONFIGURATION
        private readonly string _bookmarksPath = "bookmarks.json";
        // FILE PATH FOR STORING HISTORY CONFIGURATION
        private readonly string _historyPath = "history.json";
        // DEFAULT HOMEPAGE URL IF CONFIG FILE DOES NOT EXIST
        private string _homeUrl = "https://www.hw.ac.uk/dubai";
        // CURRENTLY VIEWING URL
        private string currentUrl = "";

        // LIST THAT STORES ALL THE BROWSING HISTORY ENTRIES 
        private List<string> history = new();
        // USED TO STORE PREVIOUS PAGE FOR THE BACKWARD NAVIGATION
        private Stack<string> backStack = new();
        // USED TO STORE NEXT PAGE FOR THE FORWARD NAVIGATION
        private Stack<string> forwardStack = new();
        // LIST THAT STORES BOOKMARKS
        private List<Bookmark> bookmarks = new();
        // MANUAL DICTIONARY USED FOR ASSIGNING READABLE TITLES FOR SPECIFIC WEBSITE
        private readonly Dictionary<string, string> _manualTitles = new()
        {
            { "https://www.hw.ac.uk/dubai", "Heriot-Watt University Dubai" },
            { "https://www.hw.ac.uk/", "Heriot-Watt University" },
            { "https://www.hw.ac.uk", "Heriot-Watt University" }
        };

        // CONSTRUCTORS FOR THE MAINWINDOW CLASS
        public MainWindow()
        {
            // INITIALIZING ALL UI ELEMENTS IN THE .AXAML FILES
            InitializeComponent();
            // LOADING CONFIGURATION FILE FOR HOMEPAGE
            LoadConfig();
            // LOADING BOOKMARKS FROM THE FILE
            LoadBookmarks();
            // UPDATING THE BOOKMARK LIST DISPLAYED IN THE UI
            UpdateBookmarksList();
            // LOADING BROWSING HISTORY FROM THE FILE
            LoadHistory();
            // UPDATES HISTORY LIST DISPLAYED IN THE UI
            UpdateHistoryList();

            // HISTORY EXIST LOGIC
            // CHECKS IF ANY HISTORY EXISTS  
            // IF HISTORY EXISTS
            if (history.Count > 0)
            {
                // GETS THE LAST VISITED URL FROM HISTORY.JSON FILE
                var lastUrl = ExtractUrlFromHistoryEntry(history.Last());
                // NAVIGATES TO THE LAST VISITED URL
                NavigateNewAsync(lastUrl);
                // SETS THAT URL TO THE VALUE
                currentUrl = lastUrl;
            }
            // IF HISTORY DOES NOT EXIST
            else
            {
                // LOADS THE HOMEPAGE
                _ = NavigateNewAsync(_homeUrl);
            }

            // BUTTON CLICKS
            // USED FOR ASSIGNING THE BUTTON CLICKS TO THEIR RESPECTIVE METHODS
            GoButton.Click += async (_, __) => await NavigateNewAsync(UrlBox.Text);
            HomeButton.Click += async (_, __) => await NavigateNewAsync(_homeUrl);
            SetHomeButton.Click += (_, __) => SaveConfig(UrlBox.Text);
            AddBookmarkButton.Click += (_, __) => AddBookmark(UrlBox.Text);
            ShowBookmarksButton.Click += ToggleBookmarksVisibility;
            RefreshButton.Click += async (_, __) => await RefreshPage();
            ShowHistoryButton.Click += ToggleHistoryVisibility;
            ClearHistoryButton.Click += (_, __) => ClearHistory();
            BackButton.Click += async (_, __) => await GoBack();
            ForwardButton.Click += async (_, __) => await GoForward();
        }

        // ================== PAGE LOADING SECTION ==================
        // USED FOR GETTING THE HTML CONTENT, TITLE, AND THE STATUS CODE FOR THE GIVEN URL
        private async Task<(string status, string html, string title, string url)> FetchAsync(string url)
        {
            // ENSURES THAT THE URL BEGINS WITH HTTP 
            // THIS MAKES SURE THAT ITS A VALID REQUEST
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                url = "https://" + url;

            // EXPECTED OUTPUT
            try
            {
                //SENDS A GET REQUEST FOR THE URL
                var response = await _httpClient.GetAsync(url);
                // READS THE FULL HTML CONTENT OF THE PAGE
                string html = await response.Content.ReadAsStringAsync();
                // EXTRACTING STATUS CODE FOR DISPLAYING
                string status = $"{(int)response.StatusCode} {response.ReasonPhrase}";
                // EXTRACTING THE TITLE TAG FOR DISPLAYING
                string title = ExtractTitle(html, url);
                //RETURNS THE GATHERED DATA 
                return (status, html, title, url);
            }
            // IF THERE IS AN ISSUE IN THIS STEP
            // MEANING IF THE PAGE FAILS TO LOAD
            catch (Exception ex)
            {
                // THROWING AN EXCEPTION WITH AN ERROR MESSAGE
                // NOTHING DISPLAYS IN THE HTML DISPLAY BOX
                return ($"Error: {ex.Message}", "Error loading page.", "Error", url);
            }
        }
        // NAVIGATING TO A NEW URL
        // THIS INCLUDIS THE UPDATING OF UI, HISTORY AND STATE
        private async Task NavigateNewAsync(string url)
        {
            // RETURNS IF THERE IS NOT ANY URL ENTERED
            if (string.IsNullOrWhiteSpace(url)) return;
            // FORMATS THE URL PROPERLY IF ITS INCOMPLETE
            url = NormalizeUrl(url);

            // IF A PAGE IS ALREADY BEING VIEWED, THIS LOGIC GETS APPLIED
            if (!string.IsNullOrEmpty(currentUrl) && !string.Equals(currentUrl, url, StringComparison.OrdinalIgnoreCase))
            {
                // MOVING THE CURRENT URL TO BACKSTACK
                backStack.Push(currentUrl);
                // FORWARD STACK IS MADE CLEAR
                forwardStack.Clear();
            }

            // FETCHING THE HTML. STATUS, AND TITLE FOR THE NEW URL
            var (status, html, title, finalUrl) = await FetchAsync(url);
            // RENDERING THE CONTENT IN THE BROWSER UI
            RenderPage(status, html, title);
            // UPDATING THE NEW URL AS THE FINAL URL
            currentUrl = finalUrl;
            // UPDATING THE URL INTO THE TEXTBOX
            UrlBox.Text = finalUrl;

            // THE NEW PAGE GETS ADDED TO THE HISTORY
            // IT ALSO CHECKS IF THE PREV URL IS A COPY/DUPLICATE
            AppendHistoryIfNotDuplicate(finalUrl, status);
            // SAVES UPDATED HISTORY INTO FILE
            SaveHistory();
            // UPDATES THE HISTORY LIST DISPLAY
            UpdateHistoryList();
            // UPDATES THE NAVGATION BUTTONS STATE
            UpdateNavButtons();
        }

        // RESPONSIBLE FOR RENDERING THE PAGE HTML, TITLE, LINK LIST TO UI COMPONENTS 
        private void RenderPage(string status, string html, string title)
        {
            // PROVIDED A LIMIT FOE DISPLAYING HTML
            HtmlDisplay.Text = html.Length > 12000 ? html[..12000] + "\n\n[Preview truncated]" : html;
            // DISPLAYING THE HTTP STATUS CODE
            StatusBlock.Text = $"Status: {status}";
            // DISPLAYING THE WEBPAGE TITLE
            TitleBlock.Text = $"Title: {title}";
            // DISPLAYING THE FIRST 5 HYPERLINKS FROM THE HTML
            DisplayFirstFiveLinks(html);
        }

        // ================== NAVIGATION SECTION ==================
        // CONSIST OF GOING PREVIOUS PAGE AND THE NEXT PAGE THAT IS PRESENT IN THE SEARCHED URLS PRESENT IN THE HISTORY

        // NAVIGATES ONE STEP BACKWARD IN THE BROWSING HISTORY
        private async Task GoBack()
        {
            // IF THERE IS NO PREVIOUS PAGES
            if (backStack.Count == 0)
            {
                // IT RETURNS WITH A MESSAGE THAT THERES NO PREVIOUS PAGE IN THE HISTORY
                StatusBlock.Text = "No previous page in history.";
                return;
            }
            // PUSHING CURRENT PAGE INTO FORWARD STACK 
            // DONE FOR NAVIGATING AGAIN
            forwardStack.Push(currentUrl);
            // RETRIEVING THE PREVIOUS PAGE FROM THE STACK
            string target = backStack.Pop();
            // LOADING THE PREVIOUS PAGE AGAIN
            // FETCHING THE HTML, STATUS, AND TITLE OF THAT PREVIOUS URL
            var (status, html, title, finalUrl) = await FetchAsync(target);
            // RENDERING THE CONTENT IN THE BROWSER UI
            RenderPage(status, html, title);
            // UPDATING THE NEW URL AS THE FINAL URL 
            currentUrl = finalUrl;
            // UPDATING THE URL INTO THE TEXTBOX
            UrlBox.Text = finalUrl;

            // THIS PAGE GETS ADDED TO THE HISTORY
            // IT ALSO CHECKS IF THE PREV URL IS A COPY/DUPLICATE
            AppendHistoryIfNotDuplicate(finalUrl, status);
            // SAVES UPDATED HISTORY INTO FILE
            SaveHistory();
            // UPDATES THE HISTORY LIST DISPLAY
            UpdateHistoryList();
            // UPDATES THE NAVGATION BUTTONS STATE
            UpdateNavButtons();
        }

        // NAVIGATES ONE STEP FORWARD IN THE BROWSING HISTORY
        private async Task GoForward()
        {
            // IF THERE IS NO NEXT PAGES
            if (forwardStack.Count == 0)
            {
                // IT RETURNS WITH A MESSAGE THAT THERES NO NEXT PAGE IN THE HISTORY
                StatusBlock.Text = "No next page in history.";
                return;
            }

            // PUSHES THAT URL INTO THE BACKWARD STACK
            // DONE FOR NAVIGATING AGAIN   
            backStack.Push(currentUrl);
            // RETRIEVING THE NEXT PAGE FROM THE STACK
            string target = forwardStack.Pop();
            // LOADING THE NEXT PAGE 
            // FETCHING THE HTML, STATUS, AND TITLE OF THAT NEXT URL
            var (status, html, title, finalUrl) = await FetchAsync(target);
            // RENDERING THE CONTENT IN THE BROWSER UI
            RenderPage(status, html, title);
            // UPDATING THE NEW URL AS THE FINAL URL
            currentUrl = finalUrl;
            // UPDATING THE URL INTO THE TEXTBOX
            UrlBox.Text = finalUrl;

            // THIS PAGE GETS ADDED TO THE HISTORY
            // IT ALSO CHECKS IF THE PREV URL IS A COPY/DUPLICATE
            AppendHistoryIfNotDuplicate(finalUrl, status);
            // SAVES UPDATED HISTORY INTO FILE
            SaveHistory();
            // UPDATES THE HISTORY LIST DISPLAY
            UpdateHistoryList();
            // UPDATES THE NAVGATION BUTTONS STATE
            UpdateNavButtons();
        }

        // REFRESH PAGE
        // REFRESHES THE CURRENTLY OPEN PAGE
        private async Task RefreshPage()
        {
            // CHECKS IF THERE IS A URL OR NOT
            // IF THERE IS NO URL PRESENT
            if (string.IsNullOrWhiteSpace(currentUrl))
            {
                // IT DISPLAYS A MESSAGE THAT THERE IS NO PAGE TO REFRESH
                StatusBlock.Text = "No page to refresh.";
                return;
            }

            // UPDATES THE STATUS TEXT WITH A MESSAGE
            // REFRESHING...
            StatusBlock.Text = "Refreshing...";
            // WAITS FOR 1 SECOND
            await Task.Delay(1000);
            // RELOADS THE SAME URL 
            var (status, html, title, _) = await FetchAsync(currentUrl);
            // RENDERING THE CONTENT IN THE BROWSER UI 
            RenderPage(status, html, title);
            // UPDATES THE STATUS WITH A MESSAGE
            // PAGE REFRESHED.
            StatusBlock.Text = "Page refreshed.";
        }

        // NAVIGATION UPDATING FOOD
        // IT ENABLES OR DISABLES THE BUTTON ACCORDING TO THE AVAILABLITIY URLS IN THE HISTORY
        private void UpdateNavButtons()
        {
            // BACKWARD BUTTON GETS ENABLED IF THE THE COUNT OF THE BACKSTACK IS GREATER THAN 0
            BackButton.IsEnabled = backStack.Count > 0;
            // FORWARD BUTTON GETS ENABLED IF THE COUNT OF THE FORWARDSTACK IS GREATER THAN 0
            ForwardButton.IsEnabled = forwardStack.Count > 0;
            // ELSE THE BUTTON CHANGES COLOR AND GETS HIDDEN
        }

        // ================== HISTORY SECTION ==================
        // CONSIST OF ALL THE FUNCTIONS RELATED TO HISTORY
        // LOAD HISTORY IS USED FOR LOADING THE BROWSING HISTORY FROM HISTORY.JSON
        private void LoadHistory()
        {
            // CHECKS IF THE FILE EXISTS 
            if (File.Exists(_historyPath))
            {
                try
                {
                    // READS AND DESERIALIZES JSON INTO A LIST OF STRINGS
                    // JSONSERIALIZER IS USED FOR READING THE DATA AND TRANSFORMING IT INTO LIST FOR CODING
                    history = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_historyPath)) ?? new();
                }
                // RESETS IF THE FILE IS CORRUPTED
                // ALSO CHECKS IF THERE EXISTS A FILE. IF NOT THEN IT CREATES THE FILE
                catch { history = new(); }
            }
            // UPDATES THE VISIBLE LIST
            UpdateHistoryList();
        }

        // SAVES THE BROWSING HISTORY BACK INTO JSON FILE
        private void SaveHistory()
        {
            // THIS CONVERTS THE LIST INTO JSON FORMAT AND THEN ONLY STORED INTO THE JSON FILE
            File.WriteAllText(_historyPath, JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true }));
        }

        // METHOD CHECKS FOR DUPLICATE ENTRIES
        // IF THERE IS DUPLICATE ENTRY PREVIOUSLY, IT DOES NOT GET ADDED TO THE HISTORY. 
        // IF THERE IS NO DUPLICATE ENTRY PREVIOUSLY, IT GETS ADDED TO THE HISTORY.
        private void AppendHistoryIfNotDuplicate(string url, string status)
        {
            // CHECKING IF THE LAST VISITED URL = THE CURRENT URL
            // CHECKED FOR PREVENTING DUPLICATE ENTRIES
            if (history.Count > 0)
            {
                // IT DOES NOTHING IF LAST VISITED URL = CURRENT URL
                var last = ExtractUrlFromHistoryEntry(history.Last());
                if (string.Equals(last, url, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            // IF LAST VISITED URL != CURRENT URL
            // IT GETS STORED 
            // FORMAT = 'DATE TIME - [STATUS CODE] WEBSITE URL'
            string entry = $"{DateTime.Now:G} — [{status}] {url}";
            // NEW ENTRY ADDED
            history.Add(entry);
        }

        // USED FOR UPDATING THE HISTORY IN THE GUI 
        // THIS ALLOWS USER TO SEE THE HISTORY WITHIN THE WEB BROWSER GUI
        private void UpdateHistoryList()
        {
            // NULL CHECK
            // CHECKED IF THERE IS NO PANEL 
            if (HistoryList == null) return;
            // CLEARS ANY OLD ENTRIES FROM THE DISPLAY
            HistoryList.Children.Clear();

            // LOOPING THROUGH EACH HISTORY ENTRY
            foreach (var entry in history)
            {
                // EXTRACTING THE URL POART FROM FULL ENTRY TEXT
                string url = ExtractUrlFromHistoryEntry(entry);
                // CREATING A CLICKABLE BUTTON FOR EACH ENTRY
                var btn = new Button { Content = entry, Width = 580, Height = 30 };
                // PROVIDING THE COMMAND
                // IF A HISTORY URL BUTTON IS CLICKED, WEB BROWSER SEARCHES FOR THAT SPECIFIC URL BY AUTOMATICALLY ADDING THE URL INTO TEXT BOX
                btn.Click += async (_, __) => await NavigateNewAsync(url);
                // ADDING THE BUTTON INTO HISTORY PANEL ON THE GUI
                HistoryList.Children.Add(btn);
            }
        }

        // METHOD FOR EXTRACTING THE URL PART FROM THE HISTORY ENTRY
        // FORMAT - 'DD/MM/YYYY HH-MM-SS - [STATUS CODE] URL'
        private string ExtractUrlFromHistoryEntry(string entry)
        {
            // LOCATING WHERE THE URL STARTS
            int idx = entry.LastIndexOf("] ");
            return (idx != -1 && idx + 2 < entry.Length) ? entry[(idx + 2)..] : entry;
        }

        // METHOD USED FOR CLEARING THE ENTIRE BROWSING HISTORY
        private void ClearHistory()
        {
            // REMOVING ALL THE HISTORY AND RESETTING THE NAVIGATION STACKS
            history.Clear();
            backStack.Clear();
            forwardStack.Clear();
            // SAVING THE NEW HISTORY LIST BACK TO THE FILE
            SaveHistory();
            // UPDATING THE HISTORY LIST INTO THE UI 
            UpdateHistoryList();
            // TEXT MESSAGE SAYING THE HISTORY IS CLEARED
            StatusBlock.Text = "History cleared.";
            // UPDATING THE NAVIGATION BUTTON
            UpdateNavButtons();
        }

        // METHOD RESPONSIBLE FOR SHOWING AND HIDING HISTORY PANEL IN THE WEB BROWSER GUI
        private void ToggleHistoryVisibility(object? sender, RoutedEventArgs e)
        {
            // CHECKING IF HISTORY PANEL EXISTS
            if (HistoryPanel == null) return;
            // CHANGING VISIBILITY OF THE HISTORY
            HistoryPanel.IsVisible = !HistoryPanel.IsVisible;
            // UPDATING THE BUTTON TEXT AND COLOR BASED ON THE VISIBILITY
            // DURING SHOW HISTORY = BLUE BACKGROUND AND BLACK TEXT
            // DURING HIDE HISTORY = BLUE BACKGROUND AND BLACK TEXT
            // ON HOVER = GREY BACKGROUND AND WHITE TEXT
            ShowHistoryButton.Content = HistoryPanel.IsVisible ? "Hide History" : "Show History";
        }

        // ================== BOOKMARKS ==================
        // CONSIST OF ALL THE METHODS RESPONSIBLE FOR BOOKMARKS
        // DEFINING A BOOKMARK
        private class Bookmark { public string Name { get; set; } = ""; public string Url { get; set; } = ""; }

        // LOADING ALL THE BOOKMARKS FROM JSON FILE
        private void LoadBookmarks()
        {
            // CHECKING IF THE JSON FILE EXISTS
            if (File.Exists(_bookmarksPath))
            {
                try
                {
                    // READS AND DESERIALIZES JSON INTO A LIST OF STRINGS
                    // JSONSERIALIZER IS USED FOR READING THE DATA AND TRANSFORMING IT INTO LIST FOR CODING
                    bookmarks = JsonSerializer.Deserialize<List<Bookmark>>(File.ReadAllText(_bookmarksPath)) ?? new();
                }
                // RESETS IF THE FILE IS CORRUPTED
                // ALSO CHECKS IF THERE EXISTS A FILE. IF NOT THEN IT CREATES THE FILE 
                catch { bookmarks = new(); }
            }
        }

        // METHOD USED FOR SAVING ALL THE BOOKMARKS INTO THE JSON FILE
        private void SaveBookmarks()
        {
            // THIS CONVERTS THE LIST INTO JSON FORMAT AND THEN ONLY STORED INTO THE JSON FILE
            File.WriteAllText(_bookmarksPath, JsonSerializer.Serialize(bookmarks, new JsonSerializerOptions { WriteIndented = true }));
        }

        // METHOD FOR ADDING NEW BOOKMARKS INTO THE LIST
        private void AddBookmark(string url)
        {
            // CHECKS IF URL EXIST OR NOT
            // DOES NOTHING
            if (string.IsNullOrWhiteSpace(url)) return;
            // CHECKS FOR HTTPS:// IF ITS THERE BEFORE THE URL OR NOT
            // ADDS HTTPS:// IF IT DOES NOT EXIST
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) url = "https://" + url;

            // CHECKS IF THERE IS A BOOKMARK ALREADY EXISTING IN THAT URL
            if (bookmarks.Exists(b => b.Url.Equals(url, StringComparison.OrdinalIgnoreCase)))
            {
                // RETURNS A TEXT MESSAGE SAYING BOOKMARK ALREADY EXISTS
                StatusBlock.Text = "Bookmark already exists.";
                return;
            }
            // ADDS THE URL AS A NEW BOOKMARK
            // USUALLY ADDED AS BOOKMARK 1, BOOKMARK 2, ...
            bookmarks.Add(new Bookmark { Name = $"Bookmark {bookmarks.Count + 1}", Url = url });
            // SAVING THE BOOKMARKS INTO THE LIST
            SaveBookmarks();
            // UPDATING THE BOOKMARKS INTO THE LIST
            UpdateBookmarksList();
            // STATUS PROVIDES A MESSAGE SAYING BOOKMARK ADDED <URL>
            StatusBlock.Text = $"Bookmark added: {url}";
        }

        // METHOD RESPONSIBLE FOR UPDATING THE BOOKMARK 
        private void UpdateBookmarksList()
        {
            // CHECKS IF THE BOOKMARKS LIST EXIST 
            if (BookmarksList == null) return;
            // CLEARS ANY EXISTING BOOKMARKS THAT ARE SHOWN BEFORE
            BookmarksList.Children.Clear();

            // GOES THROUGH EACH BOOKMARK PRESENT IN THE LIST ONE BY ONE
            foreach (var bm in bookmarks.ToList())
            {
                // CREATING A HORIZONTAL LAYOUT FOR THE BOOKMARK
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                // CONVERTING THE STORED BOOKMARKS AS BUTTONS
                // THIS IS DONE SO THAT USERS CAN JUST VISIT THE URL PRESENT IN THE BOOKMARK BY CLICKING ON IT
                var openBtn = new Button { Content = bm.Name, Width = 200, Height = 30 };
                // PROVIDING A FUNCTION FOR THE BUTTON TO MAKE IT WORK
                openBtn.Click += async (_, __) => await NavigateNewAsync(bm.Url);

                // MODIFICATION BUTTONS FOR THE BOOKMARK
                // CREATING A BUTTON FOR RENAMING THE NAME OF THE BOOKMARK PRESENT IN THE BOOKMARK LIST
                var renameBtn = new Button { Content = "Rename", Width = 80, Height = 30 };
                // PROVIDING A FUNCTION FOR THE BUTTON TO MAKE IT WORK
                renameBtn.Click += (_, __) => RenameBookmarkDialog(bm);
                // CREATING A BUTTON FOR CHANGING THE URL OF THE BOOKMARK PRESENT IN THE BOOKMARK LIST
                var changeUrlBtn = new Button { Content = "Change URL", Width = 100, Height = 30 };
                // PROVIDING A FUNCTION FOR THE BUTTON TO MAKE IT WORK
                changeUrlBtn.Click += (_, __) => ChangeBookmarkUrlDialog(bm);
                // CREATING A BUTTON FOR DELETING THE URL OF THE BOOKMARK PRESENT IN THE BOOKMARK LIST
                var deleteBtn = new Button { Content = "Delete", Width = 80, Height = 30 };
                // PROVIDING A FUNCTION FOR THE BUTTON TO MAKE IT WORK
                deleteBtn.Click += (_, __) =>
                {
                    // REMOVING THE BOOKMARKS FROM THE LIST
                    bookmarks.Remove(bm);
                    // SAVING THE BOOKMARKS INTO JSON FILE
                    SaveBookmarks();
                    // UPDATING THE BOOKMARK LIST INTO THE JSON FILE
                    UpdateBookmarksList();
                };

                //ADDING THE BUTTONS IN A ROW 
                // OPEN BUTTON
                row.Children.Add(openBtn);
                // RENAME BUTTON
                row.Children.Add(renameBtn);
                // CHANGE URL BUTTON                
                row.Children.Add(changeUrlBtn);
                // DELETE BUTTON
                row.Children.Add(deleteBtn);
                // ADDING THE COMPLETE ROW TO THE BOOKMARK LIST PANEL
                BookmarksList.Children.Add(row);
            }
        }

        // OPENS NEW WINDOW FOR RENAMING THE BOOKMARK 
        private void RenameBookmarkDialog(Bookmark bm)
        {
            // CREATING A NEW WINDOW FOR RENAMING
            var dlg = new Window
            {
                // WINDOW TITLE, WIDTH, LENGTH, AND THE MAKING THE WINDOW OPEN IN THE CENTER
                Title = "Rename Bookmark",
                Width = 350,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            // INPUT BOX SHOWING THE CURRENT NAME
            var input = new TextBox { Text = bm.Name, Margin = new Thickness(10) };
            // BUTTON FOR CONFIRMING THE NEW NAME
            var ok = new Button { Content = "OK", Margin = new Thickness(10), HorizontalAlignment = HorizontalAlignment.Center };
            // PROVIDING A FUNCTION FOR THE BUTTON TO MAKE IT WORK
            ok.Click += (_, __) =>
            {
                // ACCEPTING THE NAME ENTERED BY THE USER
                var newName = input.Text?.Trim();
                // IF USER PROVIDED A VALID NAME
                if (!string.IsNullOrWhiteSpace(newName))
                {
                    // BOOKMARK NAME GETS UPDATED TO THE NEW NAME
                    bm.Name = newName;
                    // SAVES THE BOOKMARK NAME INTO THE JSON FILE
                    SaveBookmarks();
                    // UPDATES THE CHANGES INTO THE WEB BROWSER GUI
                    UpdateBookmarksList();
                    // SHOWS A TEXT MESSAGE 
                    // RENAMED TO <NEW NAME>
                    StatusBlock.Text = $"Renamed to {newName}";
                }

                // CLOSING THE RENAME WINDOW
                dlg.Close();
            };

            // RENAMING WINDOW (LABEL + INPUT + BUTTON)
            dlg.Content = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = "Enter new bookmark name:", Margin = new Thickness(10,5,10,0) },
                    input,
                    ok
                }
            };
            // SHOWING THE DIALOG BOX
            dlg.Show(this);
        }

        // OPENING UP A SEPARATE WINDOW FOR CHANGING THE BOOKMARK URL
        private void ChangeBookmarkUrlDialog(Bookmark bm)
        {
            // CREATING A NEW WINDOW FOR CHANGING THE URL 
            var dlg = new Window
            {
                // WINDOW TITLE, WIDTH, LENGTH, AND THE MAKING THE WINDOW OPEN IN THE CENTER
                Title = "Change Bookmark URL",
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            // INPUT BOX SHOWING THE CURRENT URL
            var input = new TextBox { Text = bm.Url, Margin = new Thickness(10) };
            // BUTTON FOR CONFIRMING THE NEW URL
            var ok = new Button { Content = "OK", Margin = new Thickness(10), HorizontalAlignment = HorizontalAlignment.Center };
            // PROVIDING A FUNCTION FOR THE BUTTON TO MAKE IT WORK
            ok.Click += (_, __) =>
            {
                // ACCEPTING THE URL ENTERED BY THE USER
                var newUrl = input.Text?.Trim();
                // IF USER PROVIDED A VALID URL
                if (!string.IsNullOrWhiteSpace(newUrl))
                {
                    // BOOKMARK URL GETS UPDATED TO THE NEW URL
                    bm.Url = NormalizeUrl(newUrl);
                    // SAVES THE BOOKMARK URL INTO THE JSON FILE
                    SaveBookmarks();
                    // SHOWS A TEXT MESSAGE 
                    // BOOKMARK URL UPDATED                    
                    UpdateBookmarksList();
                    StatusBlock.Text = "Bookmark URL updated.";
                }
                // CLOSING THE UPDATING WINDOW
                dlg.Close();
            };

            // URL UPDATING WINDOW (LABEL + INPUT + BUTTON)
            dlg.Content = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = "Enter new URL for the bookmark:", Margin = new Thickness(10,5,10,0) },
                    input,
                    ok
                }
            };
            // SHOWING THE DIALOG BOX
            dlg.Show(this);
        }

        // METHOD RESPONSIBLE FOR SHOWING AND HIDING BOOKMARK PANEL IN THE WEB BROWSER GUI
        private void ToggleBookmarksVisibility(object? sender, RoutedEventArgs e)
        {
            // CHECKING IF BOOKMARK PANEL EXISTS
            if (BookmarksPanel == null) return;
            // CHANGING VISIBILITY OF THE BOOKMARK PANEL
            BookmarksPanel.IsVisible = !BookmarksPanel.IsVisible;
            // UPDATING THE BUTTON TEXT AND COLOR BASED ON THE VISIBILITY
            // DURING SHOW BOOKMARK = BLUE BACKGROUND AND BLACK TEXT
            // DURING HIDE BOOKMARK = BLUE BACKGROUND AND BLACK TEXT
            // ON HOVER = GREY BACKGROUND AND WHITE TEXT
            ShowBookmarksButton.Content = BookmarksPanel.IsVisible ? "Hide Bookmarks" : "Show Bookmarks";
        }

        // ================== LINKS ==================
        // FUNCTION FINDS AND DISPLAYS THE FIRST 5 LINKS FOUND FROM THE WEBPAGE HTML
        private void DisplayFirstFiveLinks(string html)
        {
            // CHECKS IF THE LINKS PANEL EXIST
            // IF IT EXISTS, IT PROCEEDS TO THE NEXT PART
            // IF IT DOES NOT EXIST, IT STOPS THE FUNCTION
            if (LinksPanel == null) return;
            // CLEARS ALL OLD LINKS THAT WERE DISPLAYED PREVIOUSLY
            LinksPanel.Children.Clear();

            // FINDING THE FIRST 5 LINKS IN THE HTML USING A REGULAR EXPRESSION PATTERN
            var matches = Regex.Matches(html, @"<a\s+(?:[^>]*?\s+)?href\s*=\s*['""](.*?)['""]", RegexOptions.IgnoreCase)
                // CONVERT THE MATCHES TO A LIST THAT CAN BE LOOPED THROUGH THEM
                .Cast<Match>()
                // EXTRACTING ONLY THE URL PART FROM THE <a> TAG
                .Select(m => m.Groups[1].Value)
                // IGNORING ANY EMPTY OR INVALID URLS
                .Where(h => !string.IsNullOrWhiteSpace(h))
                // REMOVING ANY DUPLICATE LINKS
                .Distinct()
                // TAKING ONLY THE VERY FIRST 5 LINKS
                .Take(5)
                // STORING THEM IN A LIST FORMAT
                .ToList();

            // CHECKING IF THERE ARE ANY LINKS EXISTING
            // IF THERE ARE NO LINKS EXISTING
            if (matches.Count == 0)
            {
                // DISPLAYS A TEXT MESSAGE NO LINKS FOUND IN THIS PAGE
                LinksPanel.Children.Add(new TextBlock { Text = "No links found on this page." });
                return;
            }
            
            // IF THERE ARE LINKS EXISTING
            foreach (var link in matches)
            {
                // CONVERTING THEM INTO CLICKABLE BUTTONS
                // MAKING IT EASIER FOR USERS TO CLICK ON THE LINKS
                var btn = new Button
                {
                    // PROVIDING TEXT, WIDTH, HEIGHT, AND MARGIN FOR THE BUTTONS
                    Content = link,
                    Width = 580,
                    Height = 30,
                    Margin = new Thickness(0, 3, 0, 3)
                };

                // PROVIDING A FUNCTION FOR THE BUTTON TO MAKE IT WORK 
                btn.Click += async (_, __) => await NavigateNewAsync(link);
                // ADDING THE NEW BUTTON INTO LINKS PANEL SO THAT IT APPEARS IN THE APP
                LinksPanel.Children.Add(btn);
            }
        }

        // ================== CONFIG / UTILS ==================
        // LOADING THE HOME URL FROM THE CONFIG.JSON FILE WHEN THE PROGRAM STARTS
        private void LoadConfig()
        {
            // CHECKING IF THE JSON FILE EXIST
            if (File.Exists(_configPath))
            {
                try
                {
                    // READS ALL THE FILE CONTENTS AS AN TEXT
                    var json = File.ReadAllText(_configPath);
                    // CONVERTING THE JSON TEXT INTO AN OBJECT (CONFIG DATA)
                    var cfg = JsonSerializer.Deserialize<ConfigData>(json);
                    // IF HOMEPAGE VALUE EXISTS, THEN USE IT
                    if (cfg?.Homepage != null) _homeUrl = cfg.Homepage;
                }
                // ELSE USE THE DEFAULT HOMEPAGE IF SOMETHING GOES WRONG
                // THIS CAN CONSIST OF THE FOLLOWING THINGS LIKE
                // IF THERE WAS A PREVIOUS HISTORY OF URL, ONCE THE WEBBROWSER IS CLOSED AND REOPENED, THE VERY LAST URL VISITED IS SHOWN IN THE URL TEXTBOX
                // IF THERE IS NO PREVIOUS HISTORY, HOMEPAGE GETS LOADED WHEN WEBBROWSER IS OPENED
                catch { _homeUrl = "https://www.hw.ac.uk/dubai"; }
            }
            // IF THE FILE DOES NOT EXIST, IT SAVES THE DFAULT HOME URL
            else SaveConfig(_homeUrl);
        }

        // METHOD USED FOR SAVING A NEW HOMEPAGE URL INTO THE CONFIG.JSON FILE
        private void SaveConfig(string newHome)
        {
            // IF THE INPUT IS EMPTY, IT STORES NOTHING
            if (string.IsNullOrWhiteSpace(newHome)) return;
            // CHECKS FOR HTTPS://
            // IF USER PROVIDES URL WITHOUT HTTPS://, WHILE SAVING IT GETS ADDED
            if (!newHome.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                newHome = "https://" + newHome;
            // SAVES THE NEW URL AS THE NEW HOMEPAGE URL
            _homeUrl = newHome;
            // CONVERTS ALL THE CONFIG DATA (OBJECT) TO JSON AND SAVES IT
            File.WriteAllText(_configPath, JsonSerializer.Serialize(new ConfigData { Homepage = _homeUrl }));
            // DISPLAYS A TEXT MESSAGE HOMEPAGE SAVE <URL>
            StatusBlock.Text = $"Homepage saved: {_homeUrl}";
        }

        // METHOD USED FOR MAKING SURE URL ALWAYS STARTS WITH HTTPS://
        private static string NormalizeUrl(string url)
        {
            // IF THE INPUT IS EMPTY, IT STORES NOTHING
            if (string.IsNullOrWhiteSpace(url)) return url;
            // USED FOR REMOVING THE FRONT AND BACK SPACES
            url = url.Trim();
            // CHECKS FOR HTTPS://
            // IF USER PROVIDES URL WITHOUT HTTPS://, WHILE SAVING IT GETS ADDED 
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return "https://" + url;
            // IF HTTPS:// EXISTS, IT RETURNS THE URL
            return url;
        }

        // METHOD USED FOR EXTRACTS TITLE FROM THE HTML PAGE
        private string ExtractTitle(string html, string url)
        {
            // USES A REGULAR EXPRESSION MATCHING PATTERN FOR EXTRACTING THE TITLE
            var match = Regex.Match(html, "<title>(.*?)</title>", RegexOptions.IgnoreCase);
            // IF THE TITLE IS FOUND, IT DECODES ALL HTML SYMPLS
            if (match.Success) return WebUtility.HtmlDecode(match.Groups[1].Value.Trim());
            // IF NOT FOUND BUT THE URL IS KNOWN, TITLE IS EXTRACTED FROM THERE
            if (_manualTitles.TryGetValue(url, out string t)) return t;
            // ELSE DISPLAYS A MESSAGE NO TITLE FOUND
            return "(No title found)";
        }

        // HELPER CLASSES USED FOR STORING THE HOMEPAGE DATA FOR CONFIG.JSON
        // PROPERTY USED FOR STORING THE HOMEPAGE URL
        private class ConfigData { public string Homepage { get; set; } = ""; }
    }
}
