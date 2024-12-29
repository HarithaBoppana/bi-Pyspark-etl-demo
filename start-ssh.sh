#!/bin/bash

set -e -o pipefail

mkdir -p ~/.ssh
chmod 700 ~/.ssh

ssh-add -l &>/dev/null || EXIT_CODE=$?
if [ "$EXIT_CODE" == 2 ]; then
  test -r ~/.ssh-agent && \
    eval "$(<~/.ssh-agent)" >/dev/null

  ssh-add -l &>/dev/null || EXIT_CODE=$?
  if [ "$EXIT_CODE" == 2 ]; then
    (umask 066; ssh-agent > ~/.ssh-agent)
    eval "$(<~/.ssh-agent)" >/dev/null
    # ssh-add
  fi
fi

cat "$JENKINS_SSH_KEY" | tr -d '\r' | ssh-add -

ssh-keyscan git.aws.healthgrades.zone >> ~/.ssh/known_hosts
chmod 644 ~/.ssh/known_hosts
