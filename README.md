# askfm Archiver
A cross-platform tool to archive an ask.fm user profile.

## Requirements
- .Net Core 3.0 or higher 

## Overview
The tool archives a user profile and extracts all textual infromation and any attachements in the answer or the question section.
The only information it skips are polls.

### Output Format
1. **JSON**: The parsed information is stored in a JSON format for an easy processing later on.
### JSON Properties

```
"Question":           The question text

"AuthorID":           The question's author ID if it wasn't asked anonymously

"AuthorName":         The question's author name if it wasn't asked anonymously

"Answer":             The answers text

"AnswerID":           The unique answer ID

"Date":               The date on which the question was answered
     
"Visuals":            If the answer had an attachement, the downloaded filename will be here

"Link":               A link to the answer

"CurrentPageID":      The page ID of this question

"NextPageID":         The next page [this is not set anymore: always NULL]

"Likes": 0            The number of likes this question received

"NumResponses":       The number of follow-ups

"ThreadID":           The thread ID

"VisualType":         The type of visual [img, video, or audio]
```

2. **Markdown**: A markdown file will also be generated that can be converted to HTML or PDF.
The markdown file is generated according to a pre-defined template, and it should be paired with the `markdown-pdf.css`
if you wish to convert the file to html or pdf using any of the markdown to pdf/html tools.
An example of the generated pdf can be found in the `example` folder.



## Usage
Clone the project and navigate to the folder called `Run` and 
execute the following in a command prompot or a terminal.
```
dotnet askfmArchiver.dll -u <user> -h <Markdown Page Title> [options]

OPTIONS:
 -p --page <pageID>       Start parsing at the specified page.
 
 -S --stop-at <Date>      Stops parsing once an answer that is answered earlier
                          than or the same as the specified date is reached.
                          Should follow the following format:
                          yyyy''MM''ddTHH''mm''ss [example: 2020-02-12T05:58:25]
 
 -t --threads             Creates a map between thread ids and answer ids 
                          and saves the information to disk
-h  --title               Specifies a title/header for the first page in the generated markdown file

 ```
 
 Output will be found in a folder called `output` in the same directory as the program.
