To test if you meet all dependencies for this package, follow the following instructions:

1. Download the approriate deb package for wkhtmltopdf from https://wkhtmltopdf.org/downloads.html
2. Install the package with
   
        sudo dpkg -i xxx.deb

3. Have a look at the missing dependencies and add these packages to the respective installation scripts
   - roles/lib/tasks/main.yml
   - roles/middleware/tasks/main.yml
   - roles/ui/tasks/main.yml

Note that under Ubuntu 22.04 the latest wkhtml package needs to be installed (not the one coming with the OS).
 