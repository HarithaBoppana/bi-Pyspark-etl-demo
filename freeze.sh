#!/usr/bin/env bash
echo -e "freeze.sh"

# source .env file (required by Nexus)
if [ -e ".env" ]; then 
    echo -e "\033[4m Source Environment \033[0m"
    set -a; source .env; set +a -x +e  # -x:debug mode, -e:cancel on error
fi

# freeze requirements.txt
if [ -e "constraints.txt" ]; then
    echo -e "\033[4m Freeze requirements.txt from constraints.txt \033[0m $1"
    rm -r _venv
    python3 -m venv _venv
    ./_venv/bin/pip3 install --upgrade pip
    ./_venv/bin/pip3 install -r constraints.txt
    ./_venv/bin/pip3 freeze -r constraints.txt --no-cache-dir > requirements.txt
fi
