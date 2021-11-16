Feature: Web sockets support

Scenario: Client can connect via web socket
    Given Started server with endpoint "localgost:9000"
    When Client connects to "ws://localgost:9000/ws"
        And Sends json { "Action": "Subscribe", "Topic": "xyz" }
    Then Servers keeps connection
    And Server received json { "Action": "Subscribe", "Topic": "xyz" }
    And Server received 1 packets
