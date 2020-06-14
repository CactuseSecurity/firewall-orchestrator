# Testing django/python

## installation

    IDE: install pycharm professional: sudo snap install pycharm-professional --classic (30 day free testing)

    set settings file to fwo/fwo/settings.py

    install bootstrap ui in venv - file - settings - project: fwo - project interpreter - add django-boostrap-ui, django-bootstrap-staticfiles

    run manage.py migrate (edit manage) to install admin etc.

    add user using manage.py

    copy django-bootstrap-staticfiles to absolute path /var/www/static and

    point to this dir in settings STATICFILE_DIRS var

## cons
- complex - difficult first steps
- not hot shit reactive?
- having to deal with python versions/virtualenvs
## pros
- python-based - homogeneous development across all components?
- no need for rich client distribution - any browser will do
