# askfm Archiver
A cross-platform tool to archive an ask.fm user profile.

## Requirements
- .Net Core 3.1 or higher.
- sqlite3 (optional) - Needed if you want to inspect, query, or modify the database.

## Overview
The tool archives a user profile and extracts all textual information and any attachments in the answer or the question section.
The only information it skips are polls.

## Usage
Build from source or download the zip from the release page, extract it, and execute the following in a command prompt or a terminal inside the extracted folder.
```
dotnet askfmArchiver.dll -u <user> -t <type> [OPTIONS]

OPTIONS:
  -u, --user USER        Required. The userid of the askfm account

  -t, --type TYPE        Required. Specify job type: 'parse', 'markdown'

  -p, --page ITERATOR    (Default: ) The page iterator (id) at which parsing
                         should start. Useful if parsing was interrupted.

  -s, --stop-at          The date at which parsing should stop. Date should be
                         in the following format: yyyy''MM''ddTHH''mm''ss

  -o, --out FOLDER       (Default: ./output/) Specify output folder where any
                         downloaded or generated files will be saved.

  --help                 Display help screen.

  --version              Display version information.
 ```

### Where is the parsed data stored?
Data is stored in a sqlite db file called **data.db** that gets created in the output folder once parsing starts. 

### Markdown
The tool can also generate a markdown file of the parsed data that can be converted to HTML or PDF (using another tool) if the type is set to **markdown**.
An example pdf that was created from the generated markdown file and the provided css file `markdown-pdf.css` can be found in the `example` folder.
