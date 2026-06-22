06
13

Please review this full code base again. 
My expectation here was no matter 
how many requests we get from the users who open a web browser 
no matter how many users we have 
as long as they are from the same tile, 
when they spam the refresh button for example 
Today
Mostly Sunny
95°F
Mostly sunny, with a high near 95. Heat index values as high as 101. South wind around 16 mph, with gusts as high as 25 mph. New rainfall amounts less than a tenth of an inch possible.
Precip 5%
Wind S 16 mph
Grid AKQ 83,61
the api should serve cached response for a while 
and not spam the national weather service for every single request we get 
even if they are from different browsers
different ip address 
but looks like we query NWS every time 
I did some research and there is this concept called a "stampede"? 
We should be model citizens 
because this is a sandbox / learning application 
and we should do everything correctly 
including making sure we don't stress NWS API endpoints unnecessarily 
also I noticed 
the github actions don't test my container files 
can you please add another github action 
to test the containerfiles? 
I am not sure how 
because the test should be something like the containers are able to be created and destroyed and so on 
but there is no podman on my local machine on windows, only on fedora 
please review the entire source code if there are any defects or violations of best practices 
I have also made some changes such as updating nuget package versions so please review latest dump.txt 
