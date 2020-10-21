# The data format of hits
## source
<https://sc1.checkpoint.com/documents/latest/APIs/#web/show-access-rule~v1.6.1%20>
## command

    show-access-rule
    
with header

    {
    "name" : "Rule 1",
    "layer" : "Network",
    "show-hits" : true,
    "hits-settings" : {"from-date" : "2014-01-01", "to-date" : "2014-12-31T23:59"}
    }
    
hits-settings is optional

## output

    percentage: str "x%"   with x in [0-100]
    level: str "x"   with x in {zero, low, medium, high, very high}
    value: int x
    first date: obj X   with X object that contains posix and iso-8601 dates, only available if at least one hit occured
    last date: obj X    see first date
    
# example

An example output in json format is

    "hits": {
        "percentage": "0%",
        "level": "low",
        "value": 2,
        "first-date": {
            "posix": 1602857214000,
            "iso-8601": "2020-10-16T07:06-0700"
        },
        "last-date": {
            "posix": 1602857214000,
            "iso-8601": "2020-10-16T07:06-0700"
        }
    }
