set -x -e
PYTHONPATH=$(pwd)/src:$PYTHONPATH
export PYTHONPATH
pip3 install wheel
pip3 install cython
pip3 install -qq coverage flake8 pep8-naming
pip3 install -qqr requirements.txt
pip3 install --force-reinstall -qqr requirements.txt
coverage run --source Mock_api_elt -m unittest discover -s tests
coverage report
coverage html
