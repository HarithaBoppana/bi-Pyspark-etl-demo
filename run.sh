#!/usr/bin/env bash
set +x -e  # SET: -x:debug mode, -e:cancel on error
if [ -e ".env" ]; then 
    set -a; source .env; set +a;  # source .env file
fi
./_venv/bin/python3 -m clickstream_validation_job
