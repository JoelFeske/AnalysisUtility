using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using OSIsoft.AF;
using OSIsoft.AF.Analysis;
using OSIsoft.AF.Asset;
using OSIsoft.AF.Time;
using OSIsoft.AF.PI;

namespace AnalysisUtility
{
    class Program
    {
        // Constants
        const int NumberOfValuesToReturn = int.MaxValue;
        static ConsoleColor NormalTextColor = ConsoleColor.Gray;

        static void BackfillOrRecalculateAnalyses(PISystem AFServer, AFDatabase AFDB, string searchRootPath, string targetElementMask, string elementTemplateName, string analysisName, string startTime, string endTime, bool searchFullHierarchy, string mode, ConsoleColor defaultConsoleColor)
        {
            bool recalculate = mode == "recalculate" ? true : false;
            string recalculateString = recalculate ? "recalculate" : "backfill";
            AFAnalysisService analysisService = AFServer.AnalysisService;

            // Get element template
            AFNamedCollectionList<AFElementTemplate> elementTemplates = null;
            if (elementTemplateName != null)
            {
                elementTemplates = AFElementTemplate.FindElementTemplates(AFDB, elementTemplateName, AFSearchField.Name, AFSortField.Name, AFSortOrder.Ascending, NumberOfValuesToReturn);

                // Make sure there are matching element templates
                if (elementTemplates.Count == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\nNo matching element templates found.");
                    Console.ForegroundColor = defaultConsoleColor;
                    return;
                }
            }

            // Make sure the user passed a start and end time
            if (startTime == null || endTime == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n\nYou much specify a start and end time.");
                Console.ForegroundColor = defaultConsoleColor;
                return;
            }
            // Make sure start time is after end time
            AFTime start;
            AFTime end;
            AFTime.TryParse(startTime, out start);
            AFTime.TryParse(endTime, out end);
            if (start > end)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\nStart time cannot be after end time.");
                Console.ForegroundColor = defaultConsoleColor;
                return;
            }


            // Get target elements
            AFNamedCollectionList<AFElement> targetElements = new AFNamedCollectionList<AFElement>();
            if (string.IsNullOrEmpty(elementTemplateName))
            {
                targetElements = GetTargetElementsFromPath(searchRootPath, targetElementMask, AFDB, searchFullHierarchy);
            }
            else
            {
                targetElements = GetTargetElementsFromTemplate(searchRootPath, targetElementMask, AFDB, searchFullHierarchy, elementTemplates);
            }

            // Make sure there are matching elements
            if (targetElements.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\nNo matching elements found.");
                Console.ForegroundColor = defaultConsoleColor;
                return;
            }

            // Get the analyses to be backfilled or recalculated
            List<AFAnalysis> analysesMatchingSearch = new List<AFAnalysis>();
            foreach (AFElement element in targetElements)
            {
                analysesMatchingSearch.AddRange(AFAnalysis.FindAnalyses(AFDB, analysisName, AFSearchField.Name, AFSortField.Name, AFSortOrder.Ascending, NumberOfValuesToReturn).Where(an => an.Target.Equals(element)).ToList());
            }

            // Make sure there are matching analyses
            if (analysesMatchingSearch.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\nNo matching analyses found.");
                Console.ForegroundColor = defaultConsoleColor;
                return;
            }

            // Display backfill timerange
            Console.WriteLine("\n");
            if (start == AFTime.MinValue || end == AFTime.MaxValue)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Please verify that this is the correct time range:");
                Console.ForegroundColor = NormalTextColor;
            }
            Console.Write("Time Range: ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("{0} - {1}", start, end);
            Console.ForegroundColor = NormalTextColor;

            // Verify the analyses to be backfilled
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nFound {0} analyses matching search criteria:", analysesMatchingSearch.Count);
            Console.ForegroundColor = NormalTextColor;
            List<AFAnalysis> analysesToBackfill = new List<AFAnalysis>();
            bool analysesNotEnabled = false;
            ConsoleColor pathColor = NormalTextColor;
            foreach (AFAnalysis analysis in analysesMatchingSearch)
            {
                if (analysis.GetStatus() == AFStatus.Enabled)
                {
                    analysesToBackfill.Add(analysis);
                }
                else
                {
                    analysesNotEnabled = true;
                    pathColor = ConsoleColor.Yellow;
                }
                PrintAnalysisPath(analysis, pathColor);
                pathColor = NormalTextColor;
            }
            if (analysesNotEnabled)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\nAnalyses printed in yellow are not enabled, and will not be {0}.", recalculateString == "recalculate" ? "recalculated" : "backfilled");
                Console.ForegroundColor = NormalTextColor;
            }

            Console.Write("\nContinue with {0} of {1} analyses? Y/N: ", recalculateString == "recalculate" ? "recalculation" : "backfill", analysesToBackfill.Count);
            Console.ForegroundColor = ConsoleColor.Green;
            string continueBackfill = Console.ReadLine();
            Console.ForegroundColor = NormalTextColor;
            string[] yesStrings = { "y", "yes", "uh huh" };
            if (yesStrings.Contains(continueBackfill.ToLower()))
            {
                // Backfill or recalculate analyses
                AFTimeRange timeRange = new AFTimeRange(startTime, endTime);
                AFAnalysisService.CalculationMode calculationMode;
                Console.ForegroundColor = ConsoleColor.Green;
                if (recalculate)
                {
                    Console.WriteLine("\nRecalculating {0} analyses...", analysesToBackfill.Count);
                    calculationMode = AFAnalysisService.CalculationMode.DeleteExistingData;
                }
                else
                {
                    Console.WriteLine("\nBackfilling {0} analyses...", analysesToBackfill.Count);
                    calculationMode = AFAnalysisService.CalculationMode.FillDataGaps;
                }
                Console.ForegroundColor = NormalTextColor;
                analysisService.QueueCalculation(analysesToBackfill, timeRange, calculationMode);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\nNo analyses will be {0}.", recalculateString == "recalculate" ? "recalculated" : "backfilled");
                Console.ForegroundColor = NormalTextColor;
            }

            Console.WriteLine("\nExiting");
            Console.ForegroundColor = defaultConsoleColor;
        }

        static void FindAnalysesWritingToTag(PISystem AFServer, AFDatabase AFDB, string tagName)
        {

            List<AFAnalysis> allAnalyses = AFAnalysis.FindAnalyses(AFDB, null, AFSearchField.Name, AFSortField.Name, AFSortOrder.Ascending, NumberOfValuesToReturn).ToList();
            List<AFAnalysis> analysesWritingToTag = new List<AFAnalysis>();

            AFAnalysisRuleConfiguration configuration = null;

            foreach (AFAnalysis analysis in allAnalyses)
            {
                // Get analysis configuration
                // Initially tried to use AFAnalysisRule.GetAttributeValues, but for some analyses it returns generic ouptut variable definitions simply called "output"
                // AFAnalysisRule.GetConfiguration actually resolves the output attribute names. See case 622983 entries 58 and 59.
                configuration = analysis.AnalysisRule.GetConfiguration();
                // Loop over resolved outputs to compare the output tag name to the specified tag
                foreach (AFAnalysisRuleResolvedOutput output in configuration.ResolvedOutputs)
                {
                    try
                    {
                        if (output.Attribute?.DataReference.PIPoint?.Name == tagName)
                        {
                            analysesWritingToTag.Add(analysis);
                        }
                    }
                    catch (PIException) { } // If the PIPoint hasn't been created on the Data Archive, a PIException will be thrown, but it can be ignored.
                    
                }
            }

            switch (analysesWritingToTag.Count)
            {
                case 0:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("\nNo analyses writing to '{0}'.", tagName);
                    break;
                case 1:
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("\nOne analysis writing to '{0}':", tagName);
                    Console.ForegroundColor = NormalTextColor;
                    foreach (AFAnalysis analysis in analysesWritingToTag)
                    {
                        PrintAnalysisPath(analysis, NormalTextColor);
                    }
                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\n{0} analyses writing to '{1}':", analysesWritingToTag.Count, tagName);
                    Console.ForegroundColor = NormalTextColor;
                    foreach (AFAnalysis analysis in analysesWritingToTag)
                    {
                        PrintAnalysisPath(analysis, NormalTextColor);
                    }
                    break;
            }
        }

        static AFNamedCollectionList<AFElement> GetTargetElementsFromPath(string searchRootPath, string targetElementMask, AFDatabase AFDB, bool searchFullHierarchy)
        {
            // Get all the elements in the targetElementPathMask
            if (searchRootPath != null)
            {
                char[] delimiters = { '\\', '/' };
                string[] searchRootPathStrings = { };
                searchRootPathStrings = searchRootPath.Split(delimiters);

                AFNamedCollectionList<AFElement> searchRootPathElements = new AFNamedCollectionList<AFElement>();
                AFElement elementToAdd = null;
                for (int i = 0; i < searchRootPathStrings.Length; i++)
                {
                    try
                    {
                        if (i == 0)
                        {
                            elementToAdd = AFElement.FindElements(AFDB, null, searchRootPathStrings[i], null, null, null, AFElementType.Any, false, AFSortField.Name, AFSortOrder.Ascending, 1).Single();
                        }
                        else
                        {
                            elementToAdd = AFElement.FindElements(AFDB, searchRootPathElements[i - 1], searchRootPathStrings[i], null, null, null, AFElementType.Any, false, AFSortField.Name, AFSortOrder.Ascending, 1).Single();
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("\nInvalid root path.");
                        Console.ForegroundColor = NormalTextColor;
                        return searchRootPathElements;
                    }

                    searchRootPathElements.Add(elementToAdd);
                }

                // Return a list of elements that match the targetElementMask
                return AFElement.FindElements(AFDB, searchRootPathElements.Last(), targetElementMask, null, null, null, AFElementType.Any, searchFullHierarchy, AFSortField.Name, AFSortOrder.Ascending, NumberOfValuesToReturn);
            }
            else
            {
                return AFElement.FindElements(AFDB, null, targetElementMask, null, null, null, AFElementType.Any, searchFullHierarchy, AFSortField.Name, AFSortOrder.Ascending, NumberOfValuesToReturn);
            }
        }

        static AFNamedCollectionList<AFElement> GetTargetElementsFromTemplate(string searchRootPath, string targetElementMask, AFDatabase AFDB, bool searchFullHierarchy, AFNamedCollectionList<AFElementTemplate> elementTemplates)
        {
            AFNamedCollectionList<AFElement> targetElementCandidates = GetTargetElementsFromPath(searchRootPath, targetElementMask, AFDB, searchFullHierarchy);
            AFNamedCollectionList<AFElement> targetElements = new AFNamedCollectionList<AFElement>();
            foreach (AFElement element in targetElementCandidates)
            {
                foreach (AFElementTemplate template in elementTemplates)
                {
                    if (element.Template != null && element.Template.Equals(template))
                    {
                        targetElements.Add(element);
                    }
                }
            }
            return targetElements;
        }

        static void PrintAnalysisPath(AFAnalysis analysis, ConsoleColor pathColor)
        {
            AFElement analysisTarget = (AFElement)analysis.Target;
            List<AFElement> analysisTargetPathElements = new List<AFElement>() { analysisTarget };
            List<string> analysisTargetPathStrings = new List<string>() { analysisTarget.Name };
            while (analysisTargetPathElements.Last().Parent != null)
            {
                analysisTargetPathElements.Add(analysisTargetPathElements.Last().Parent);
                analysisTargetPathStrings.Add(analysisTargetPathElements.Last().Name);
            }

            analysisTargetPathStrings.Remove(analysisTarget.Name);
            analysisTargetPathStrings.Reverse();
            foreach (string name in analysisTargetPathStrings)
            {
                Console.ForegroundColor = pathColor;
                Console.Write("{0}", name);
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("\\");
                Console.ForegroundColor = pathColor;
            }

            Console.Write(analysis.Target);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("|");
            Console.ForegroundColor = pathColor;
            Console.WriteLine(analysis.Name);
        }

        static void PrintHelp(ConsoleColor defaultConsoleColor)
        {
            Console.WriteLine("");

            // https://msdn.microsoft.com/en-us/library/b9skfh7s%28v=vs.110%29.aspx?f=255&MSPPError=-2147217396
            using (FileStream fs = File.Open(".\\help.txt", FileMode.Open))
            {
                byte[] b = new byte[1024];
                UTF8Encoding temp = new UTF8Encoding(true);

                while (fs.Read(b, 0, b.Length) > 0)
                {
                    Console.WriteLine(temp.GetString(b));
                }
            }

            Console.ForegroundColor = defaultConsoleColor;
        }

        
        static void Main(string[] args)
        {
            // Set console paramters
            ConsoleColor defaultConsoleColor = Console.ForegroundColor;
            
            Console.ForegroundColor = NormalTextColor;

            // Input parameter defaults
            string AFServerName = null;
            string AFDBName = null;
            string searchRootPath = null;
            string targetElementMask = null;
            string analysisName = null;
            string elementTemplateName = null;
            bool searchFullHierarchy = false;
            string startTime = null;
            string endTime = null;
            string mode = null;
            string outputTagName = null;

            // Parse arguments
            char[] prefixes = { '/', '-' };
            string[] argsLower = new string[args.Length];
            for (int i = 0; i < args.Length; i = i + 2)
            {
                argsLower[i] = args[i].ToLower();
                switch (argsLower[i].Trim(prefixes))
                {
                    case "afserver":
                    case "server":
                        AFServerName = args[i + 1];
                        break;
                    case "afdatabase":
                    case "database":
                    case "db":
                        AFDBName = args[i + 1];
                        break;
                    case "searchrootpath":
                    case "root":
                        searchRootPath = args[i + 1];
                        break;
                    case "elementname":
                    case "element":
                    case "elem":
                        targetElementMask = args[i + 1];
                        break;
                    case "elementtemplate":
                    case "template":
                        elementTemplateName = args[i + 1];
                        break;
                    case "analysisname":
                    case "analysis":
                        analysisName = args[i + 1];
                        break;
                    case "searchfullhierarchy":
                        searchFullHierarchy = args[i + 1].Equals("true", StringComparison.InvariantCultureIgnoreCase) ? true : false;
                        break;
                    case "starttime":
                    case "start":
                    case "st":
                        startTime = args[i + 1];
                        break;
                    case "endtime":
                    case "end":
                    case "et":
                        endTime = args[i + 1];
                        break;
                    case "tagname":
                    case "tag":
                    case "pitag":
                    case "pipoint":
                        outputTagName = args[i + 1];
                        break;
                    case "mode":
                        mode = args[i + 1];
                        break;
                    case "help":
                    case "?":
                        PrintHelp(defaultConsoleColor);
                        return;
                    default:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("\nParameter {0} not recognized.", args[i]);
                        Console.ForegroundColor = defaultConsoleColor;
                        return;
                }
            }

            if (mode == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\nPlease select a mode.");
                Console.ForegroundColor = defaultConsoleColor;
                return;
            }

            PISystems PISystems = new PISystems();
            PISystem AFServer = PISystems[AFServerName] ?? PISystems.DefaultPISystem;
            AFDatabase AFDB = AFServer.Databases[AFDBName] ?? AFServer.Databases.DefaultDatabase;
            
            Console.Write("\n\nAF Server: ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(AFServer.Name);
            Console.ForegroundColor = NormalTextColor;
            Console.Write("AF Database: ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(AFDB.Name);
            Console.ForegroundColor = NormalTextColor;

            switch(mode.ToLower())
            {
                case "findtaginputs":
                case "inputs":
                    if (outputTagName == null)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("\nPlease enter a tag name with parameter /tagname.");
                        Console.ForegroundColor = defaultConsoleColor;
                        return;
                    }

                    FindAnalysesWritingToTag(AFServer, AFDB, outputTagName);
                    break;
                case "backfill":
                case "recalculate":
                    BackfillOrRecalculateAnalyses(AFServer, AFDB, searchRootPath, targetElementMask, elementTemplateName, analysisName, startTime, endTime, searchFullHierarchy, mode, defaultConsoleColor);
                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\nMode not recognized");
                    break;
            }

            Console.ForegroundColor = defaultConsoleColor;
        }
    }
}
