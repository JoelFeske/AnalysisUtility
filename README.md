# AnalysisUtility
This command line utility can be used to backfill and recalculate analyses in bulk, or to see which analyses are writing to a certain PI Tag.

Backfilling and Recalculation:

In my time on the AF and Analytics support team, there have been many cases when customers want to backfill or recalculate their analyses in bulk. The Management tab in PI System Explorer allows you to do this, however it can be difficult to quickly single out the analyses you wish to backfill, especially in very large AF databases. For example, if you want to backfill all the analyses matching a certain name and element template, that can be done easily. But if you want to do this only for child elements of a certain element in your database, you will have to scroll through the list of matching analyses and select them one by one. 

This utility lets you avoid this tedious process by specifying a root element underneath which to search. 

Output tag verification:

Customers often call in complaining that erroneous data is being written to the output tags of their analyses. For example, they may have an analysis scheduled to run every ten minutes, but they are seeing data written every five minutes, and the data from the off times does not look like it belongs there. In these situations, it is almost always the case that another analysis somewhere in their database is writing to the same tag. Finding out which analyses these are can be quite a tedious process, with tech support engineers poring over buffer traces and database XML files to find the culprits. 

This utility allows you to enter a tag name and find the full path to all analyses that write to it.
