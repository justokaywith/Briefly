using System;using Briefly;
class SpeechPreferencesTest {
 static void Deny(Action action){bool denied=false;try{action();}catch(ArgumentException){denied=true;}if(!denied)throw new Exception("Invalid speech option accepted");}
 static int Main(){var p=new SpeechPreferences();p.Validate();if(p.Quality!="small"||p.Vocabulary!="")throw new Exception("Accurate defaults missing");p.Language="en;cmd";Deny(()=>p.Validate());p.Language="en";p.Quality="../other";Deny(()=>p.Validate());p.Quality="tiny";p.Vocabulary="name\n--file";Deny(()=>p.Validate());p.Vocabulary="a\" --file";Deny(()=>p.Validate());p.Vocabulary=new string('a',601);Deny(()=>p.Validate());Console.WriteLine("Speech preferences: accurate defaults and bounded language/model/vocabulary validation pass.");return 0;}
}
