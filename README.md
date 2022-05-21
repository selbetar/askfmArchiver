# askfm Archiver
A cross-platform tool to archive an ask.fm user profile.

## Requirements
- sqlite3 (optional) - Needed if you want to inspect, query, or modify the database.

## Overview
The tool archives a user profile and extracts all textual information and any attachment in the answer or the question section. The only information it skips are polls.

The tool can also generate a markdown file of the parsed data that can be converted to HTML or PDF (using another tool) if the type is set to **markdown**.
An example pdf that was created from the generated markdown file and the provided css file `markdown-pdf.css` can be found in the `example` folder.

# Installation

## RELEASE FILES
File|Description
:---|:---
[askfmArchiver.zip](https://github.com/selbetar/askfmArchiver/releases/latest/download/askfmArchiver.zip)|Platform-independant binary that ships with all the necessary runtime and depedencies.

The downloaded zip file can be extracted using a tool like [7zip](https://www.7-zip.org/download.html).

## DOCKER
```bash
docker run --rm -v $(pwd):/data 'TODO' --db /data/data.db --out /data/askfm-output [OPTIONS]
```

## BUILD FROM SOURCE
### Requirements:
  - .Net Core 6.0.
  - git

Execute the following commands to build the master branch locally:

```bash
$ git clone https://github.com/selbetar/askfmArchiver.git
$ cd askfmArchiver
$ dotnet publish --configuration Release --output="./bin-out" --self-contained "-p:DebugSymbols=false;DebugType=none" ./askfmArchiver/askfmArchiver.csproj
```
The generated binary should be under `./askfmArchiver/bin-out`.

# Usage and Options
In the binary directory, execute the following command:
```
./askfmArchiver -u <user> -t <type> [OPTIONS]
```

## General Options:
```
  -u, --user          Required. The userid of the askfm profile

  -o, --out           (Default: ) Specify the output folder where any downloaded or generated files will be saved.

  -c, --config        (Default: ) Specify the config folder where app configuration is saved.

  -d, --db            Path to the database file.

  --help              Display this help screen.

  --version           Display version information.
```

## Archival Options:
```
  -a, --archive       Required. Execute an archival job for the specified user.

  -p, --page          (Default: ) The page iterator (id) at which archiving should start. Useful if extraction was interrupted.

  -s, --stop-at       The date at which extraction should stop. Date should be in the following format: yyyy''MM''ddTHH''mm''ss

```

## Makrdown Options:
```
  -m, --markdown      Required. Generate markdown file(s) for the specified user.

  -D, --descending    (Default: false) Specify output folder where any downloaded or generated files will be saved.

  -r, --reset         (Default: false) Specify output folder where any downloaded or generated files will be saved.

```
