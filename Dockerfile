# syntax=docker/dockerfile:1
FROM 958306274796.dkr.ecr.us-east-1.amazonaws.com/hgd-pdt-docker-py-build-agent:0.1.07

RUN apt-get update && apt-get install -y \
    git \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY requirements.txt .

RUN --mount=type=secret,id=service_account_github_token \
    --mount=type=ssh \
    service_account_github_token=`cat /run/secrets/service_account_github_token`; \
    if [ ! -z "${service_account_github_token}" ]; then \
        git config --global "url.https://x-access-token:$service_account_github_token@github.com/.insteadOf" ssh://git@github.com/; \
    fi; \
    python3 -m pip install -r requirements.txt
COPY clickstream_validation_job clickstream_validation_job
CMD ["python3", "-m", "clickstream_validation_job"]