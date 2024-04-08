#!/bin/sh

export AWS_ACCESS_KEY_ID=dummyKey123
export AWS_SECRET_ACCESS_KEY=dummyKey123

dotnet run $(pwd)/config.ini